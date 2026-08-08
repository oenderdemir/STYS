using System.Net.Http.Json;
using System.Text.Json;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public sealed class StysAgentApiClient : IStysAgentApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public StysAgentApiClient(HttpClient http)
    {
        _http = http;
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
        return result ?? throw new InvalidOperationException("Token response was null.");
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
        return await response.Content.ReadFromJsonAsync<AgentConfigDto>(JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync("api/agent/commands", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented) return [];
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<AgentCommandDto>>(JsonOptions, cancellationToken) ?? [];
    }

    public async Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync($"api/agent/commands/{commandId}/accept", null, cancellationToken);
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
}
