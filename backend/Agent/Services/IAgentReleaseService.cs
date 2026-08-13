using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;

namespace STYS.Agent.Services;

public interface IAgentReleaseService
{
    Task<AgentCommandDto> StageUpgradeAsync(int agentId, string requestedBy, CancellationToken cancellationToken);
    Task<(AgentRelease Release, byte[] PackageBytes)> GetReleasePackageAsync(string version, string runtimeIdentifier, CancellationToken cancellationToken);
}
