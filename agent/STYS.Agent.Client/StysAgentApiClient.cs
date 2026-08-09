using System.Net.Http.Json;
using System.Text.Json;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public sealed class StysAgentApiClient : IStysAgentApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    public StysAgentApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/agent/enroll", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<AgentEnrollmentResponse>(response, "Enrollment response was null.", cancellationToken);
    }

    public async Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/agent/auth/token", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<AgentTokenResponse>(response, "Token response was null.", cancellationToken);
    }

    public async Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync("api/agent/heartbeat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync($"api/agent/config?currentVersion={currentVersion}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<AgentConfigDto>(response, "Config response was null.", cancellationToken);
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("api/agent/commands", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented) return [];
        response.EnsureSuccessStatusCode();
        return await ReadEnvelopeDataAsync<List<AgentCommandDto>>(response, "Command list response was null.", cancellationToken);
    }

    public async Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"api/agent/commands/{commandId}/accept", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"api/agent/commands/{commandId}/running", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync($"api/agent/commands/{commandId}/complete", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync($"api/agent/commands/{commandId}/fail", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsJsonAsync($"api/agent/commands/{commandId}/reject", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T> ReadEnvelopeDataAsync<T>(HttpResponseMessage response, string errorMessage, CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(JsonOptions, cancellationToken);
        if (envelope is null)
            throw new InvalidOperationException(errorMessage);

        if (!envelope.Success)
            throw new InvalidOperationException(envelope.Message ?? errorMessage);

        return envelope.Data ?? throw new InvalidOperationException(errorMessage);
    }
}
