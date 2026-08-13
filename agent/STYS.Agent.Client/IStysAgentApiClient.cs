using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Client;

public interface IStysAgentApiClient
{
    Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken);
    Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken);
    Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken);
    Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken);
    Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken);
    Task<byte[]> DownloadReleasePackageAsync(string version, string runtimeIdentifier, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<AgentPavoDeviceRegistrationResult> RegisterPavoDeviceAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<AgentPavoDeviceStatusSnapshotDto?> GetPavoDeviceStatusSnapshotAsync(AgentPavoDeviceStatusSnapshotRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken);
    Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
    Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
    Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken);
}
