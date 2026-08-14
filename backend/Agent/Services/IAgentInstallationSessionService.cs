using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentInstallationSessionService
{
    Task<IReadOnlyCollection<AgentInstallationSessionDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<AgentInstallationSessionDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<AgentInstallationSessionCreateResponse> CreateAsync(AgentInstallationSessionCreateRequest request, string createdBy, CancellationToken cancellationToken);
    Task CancelAsync(int id, string cancelledBy, CancellationToken cancellationToken);
    Task MarkOnlineFromHeartbeatAsync(int agentId, CancellationToken cancellationToken);
    Task<(string FileName, string ContentType, byte[] Content)> GetPackageAsync(int id, string baseUrl, CancellationToken cancellationToken);
}
