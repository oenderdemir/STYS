using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public sealed class StysAgentApiClient : IStysAgentApiClient
{
    private readonly HttpClient _http;
    private readonly AgentTokenStore _tokenStore;
    private readonly StysAgentClientOptions _options;
    private readonly ILogger<StysAgentApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public StysAgentApiClient(
        HttpClient http,
        AgentTokenStore tokenStore,
        IOptions<StysAgentClientOptions> options,
        ILogger<StysAgentApiClient> logger)
    {
        _http = http;
        _tokenStore = tokenStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/agent/enroll", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AgentEnrollmentResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Enrollment response was null.");
    }

    public async Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/agent/auth/token", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AgentTokenResponse>(JsonOptions, cancellationToken);
        if (result is not null)
            _tokenStore.SetToken(result);
        return result ?? throw new InvalidOperationException("Token response was null.");
    }

    public async Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _http.PostAsJsonAsync("api/agent/heartbeat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _http.GetAsync($"api/agent/config?currentVersion={currentVersion}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentConfigDto>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _http.GetAsync("api/agent/commands", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            return [];
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AgentCommandDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task SendCommandResultAsync(AgentCommandResultRequest request, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _http.PostAsJsonAsync("api/agent/commands/result", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore.HasValidToken())
            return;

        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Agent credentials are not configured.");

        await GetTokenAsync(new AgentTokenRequest
        {
            ClientId = _options.ClientId,
            ClientSecret = _options.ClientSecret,
            AgentInstanceId = _options.AgentInstanceId,
            AgentVersion = _options.AgentVersion
        }, cancellationToken);
    }
}
