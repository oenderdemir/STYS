namespace STYS.Agent.Configuration;

public interface IAgentBootstrapManagementService
{
    Task<AgentBootstrapConfiguration> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapConfiguration> SaveConfigurationAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken);
    Task<AgentBootstrapDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapConnectionTestResult> TestConnectionAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken);
}
