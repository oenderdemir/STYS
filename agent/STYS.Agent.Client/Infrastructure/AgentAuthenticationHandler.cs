using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client.Infrastructure;

public sealed class AgentAuthenticationHandler : DelegatingHandler
{
    private readonly AgentTokenStore _tokenStore;
    private readonly IAgentUnauthorizedRecoverySink _unauthorizedRecoverySink;
    private readonly StysAgentClientOptions _options;
    private readonly ILogger<AgentAuthenticationHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    private static readonly HashSet<string> SkipAuthPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/agent/enroll",
        "/api/agent/auth/token"
    };

    public AgentAuthenticationHandler(
        AgentTokenStore tokenStore,
        IAgentUnauthorizedRecoverySink unauthorizedRecoverySink,
        IOptions<StysAgentClientOptions> options,
        ILogger<AgentAuthenticationHandler> logger)
    {
        _tokenStore = tokenStore;
        _unauthorizedRecoverySink = unauthorizedRecoverySink;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (SkipAuthPaths.Contains(request.RequestUri?.AbsolutePath ?? ""))
            return await base.SendAsync(request, cancellationToken);

        var token = await GetTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            token = await RefreshTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                var retryRequest = await CloneRequestAsync(request);
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response.Dispose();
                return await base.SendAsync(retryRequest, cancellationToken);
            }

            _tokenStore.ClearToken();
            _unauthorizedRecoverySink.HandleAuthenticationLost();
            _logger.LogWarning("STYS yetkilendirmesi kaybedildi. Agent yeniden kimlik doğrulama bekliyor.");
        }

        return response;
    }

    private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore.HasValidToken())
            return _tokenStore.GetToken();

        return await RefreshTokenAsync(cancellationToken);
    }

    private async Task<string?> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            return null;

        if (!await RefreshLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
            return null;

        try
        {
            if (_tokenStore.HasValidToken())
                return _tokenStore.GetToken();

            using var authClient = new HttpClient
            {
                BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.RequestTimeoutSeconds, 1, 300))
            };
            var tokenRequest = new AgentTokenRequest
            {
                ClientId = _options.ClientId,
                ClientSecret = _options.ClientSecret,
                AgentInstanceId = _options.AgentInstanceId,
                AgentVersion = _options.AgentVersion
            };

            var response = await authClient.PostAsJsonAsync("api/agent/auth/token", tokenRequest, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AgentTokenResponse>(JsonOptions, cancellationToken);
            if (result is not null)
            {
                _tokenStore.SetToken(result);
                return result.AccessToken;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh failed.");
            return null;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(contentBytes);
            if (request.Content.Headers.ContentType is not null)
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
        }

        foreach (var header in request.Headers)
        {
            if (!header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
