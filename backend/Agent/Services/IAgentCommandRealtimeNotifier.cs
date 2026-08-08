using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentCommandRealtimeNotifier
{
    Task CommandUpdatedAsync(AgentCommandDto command, CancellationToken cancellationToken);
}
