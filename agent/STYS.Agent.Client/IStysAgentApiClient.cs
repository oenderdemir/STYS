using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public interface IStysAgentApiClient
{
    Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken);
    Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken);
    Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken);
    Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken);
    Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
    Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
    Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
}
