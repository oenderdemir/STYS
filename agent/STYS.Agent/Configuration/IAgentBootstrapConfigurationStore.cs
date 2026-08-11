namespace STYS.Agent.Configuration;

public interface IAgentBootstrapConfigurationStore
{
    Task<AgentBootstrapConfiguration> GetAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapConfiguration?> TryGetAsync(CancellationToken cancellationToken);
    Task SaveAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken);
}
