using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Upgrade;

public interface IAgentReleaseStagingService
{
    Task<AgentCommandResult> StageAsync(AgentStageUpgradeCommand command, CancellationToken cancellationToken);
}

