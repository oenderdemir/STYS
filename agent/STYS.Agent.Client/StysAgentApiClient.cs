using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public sealed class StysAgentApiClient : IStysAgentApiClient
{
    private readonly HttpClient _http;
    private readonly IOptions<StysAgentClientOptions> _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
        public string? TraceId { get; set; }
    }

    public StysAgentApiClient(HttpClient http, IOptions<StysAgentClientOptions> options)
    {
        _http = http;
        _options = options;
    }

    public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentEnrollmentResponse>(HttpMethod.Post, "api/agent/enroll", request, cancellationToken);

    public Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentEnrollmentStatusResponse>(HttpMethod.Post, "api/agent/enrollment/status", request, cancellationToken);

    public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentTokenResponse>(HttpMethod.Post, "api/agent/auth/token", request, cancellationToken);

    public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, "api/agent/heartbeat", request, cancellationToken);

    public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentConfigDto?>(HttpMethod.Get, $"api/agent/config?currentVersion={currentVersion}", null, cancellationToken);

    public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) =>
        SendForDataAsync<AgentSelfDto>(HttpMethod.Get, "api/agent/me", null, cancellationToken);

    public Task<byte[]> DownloadReleasePackageAsync(int releaseId, CancellationToken cancellationToken) =>
        SendForBinaryAsync(HttpMethod.Get, $"api/agent/releases/{releaseId}/package", cancellationToken);

    public Task<AgentPavoDeviceRegistrationResult> RegisterPavoDeviceAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentPavoDeviceRegistrationResult>(HttpMethod.Post, "api/agent/pos-devices/register", request, cancellationToken);

    public Task<AgentPavoDeviceStatusSnapshotDto?> GetPavoDeviceStatusSnapshotAsync(AgentPavoDeviceStatusSnapshotRequest request, CancellationToken cancellationToken) =>
        SendForDataAsync<AgentPavoDeviceStatusSnapshotDto?>(HttpMethod.Post, "api/agent/pos-devices/status-snapshot", request, cancellationToken);

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        var data = await SendForDataAsync<List<AgentCommandDto>>(HttpMethod.Get, "api/agent/commands", null, cancellationToken);
        return data ?? [];
    }

    public Task AcceptCommandAsync(Guid commandId, string leaseToken, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/accept", new AgentCommandLeaseRequest { LeaseToken = leaseToken }, cancellationToken);

    public Task SetRunningCommandAsync(Guid commandId, string leaseToken, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/running", new AgentCommandLeaseRequest { LeaseToken = leaseToken }, cancellationToken);

    public Task RenewCommandLeaseAsync(Guid commandId, string leaseToken, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/renew", new AgentCommandRenewRequest { LeaseToken = leaseToken }, cancellationToken);

    public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/complete", request, cancellationToken);

    public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/fail", request, cancellationToken);

    public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
        SendForVoidAsync(HttpMethod.Post, $"api/agent/commands/{commandId}/reject", request, cancellationToken);

    private async Task<T> SendForDataAsync<T>(HttpMethod method, string relativePath, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath, body);
        using var timeoutCts = CreateTimeoutCancellationTokenSource(cancellationToken);
        using var response = await _http.SendAsync(request, timeoutCts.Token);
        return await ReadDataAsync<T>(response, timeoutCts.Token);
    }

    private async Task SendForVoidAsync(HttpMethod method, string relativePath, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath, body);
        using var timeoutCts = CreateTimeoutCancellationTokenSource(cancellationToken);
        using var response = await _http.SendAsync(request, timeoutCts.Token);
        await EnsureSuccessAsync(response, timeoutCts.Token);
    }

    private async Task<byte[]> SendForBinaryAsync(HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, relativePath, body: null);
        using var timeoutCts = CreateTimeoutCancellationTokenSource(cancellationToken);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var envelope = await ReadEnvelopeAsync<object?>(response, timeoutCts.Token);
            throw CreateException(response, envelope);
        }

        return await response.Content.ReadAsByteArrayAsync(timeoutCts.Token);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath, object? body)
    {
        var request = new HttpRequestMessage(method, BuildUri(relativePath));
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = GetBaseUrl();
        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), relativePath);
    }

    private string GetBaseUrl()
    {
        var baseUrl = _options.Value.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("STYS base URL is not configured.");
        return baseUrl;
    }

    private CancellationTokenSource CreateTimeoutCancellationTokenSource(CancellationToken cancellationToken)
    {
        var timeoutSeconds = Math.Clamp(_options.Value.RequestTimeoutSeconds, 1, 300);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return cts;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var envelope = await ReadEnvelopeAsync<T>(response, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            if (envelope is not null && envelope.Success)
                return envelope.Data is not null ? envelope.Data : default!;

            throw new InvalidOperationException("STYS yanıtı beklenen veri içermiyor.");
        }

        throw CreateException(response, envelope);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var envelope = await ReadEnvelopeAsync<object?>(response, cancellationToken);
            if (envelope is not null && !envelope.Success)
                throw new InvalidOperationException(envelope.Message ?? "STYS işlemi başarısız.");
            return;
        }

        var failedEnvelope = await ReadEnvelopeAsync<object?>(response, cancellationToken);
        throw CreateException(response, failedEnvelope);
    }

    private static async Task<ApiEnvelope<T>?> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content is null)
            return null;

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static Exception CreateException(HttpResponseMessage response, object? envelope)
    {
        var message = envelope switch
        {
            ApiEnvelope<object?> typed when !string.IsNullOrWhiteSpace(typed.Message) => typed.Message!,
            _ => response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}"
        };

        var traceId = envelope switch
        {
            ApiEnvelope<object?> typed => typed.TraceId,
            _ => null
        };

        return new AgentApiException(response.StatusCode, message, traceId);
    }
}
