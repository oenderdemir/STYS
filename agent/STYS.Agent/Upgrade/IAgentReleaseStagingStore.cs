namespace STYS.Agent.Upgrade;

public interface IAgentReleaseStagingStore
{
    Task<AgentReleaseStagingState?> GetAsync(int releaseId, CancellationToken cancellationToken);
    Task UpsertAsync(AgentReleaseStagingState state, CancellationToken cancellationToken);
}
