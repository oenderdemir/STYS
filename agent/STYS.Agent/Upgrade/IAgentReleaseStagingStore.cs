namespace STYS.Agent.Upgrade;

public interface IAgentReleaseStagingStore
{
    Task<AgentReleaseStagingState?> GetAsync(string version, string runtimeIdentifier, CancellationToken cancellationToken);
    Task UpsertAsync(AgentReleaseStagingState state, CancellationToken cancellationToken);
}

