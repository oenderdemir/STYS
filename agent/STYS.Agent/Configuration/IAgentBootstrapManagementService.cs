namespace STYS.Agent.Configuration;

public interface IAgentBootstrapManagementService
{
    Task<AgentBootstrapConfiguration> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapConfigurationSaveResult> SaveConfigurationAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken);
    Task<AgentBootstrapDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapDiagnosticsDto> GetDiagnosticsAsync(CancellationToken cancellationToken);
    Task<AgentBootstrapResetResult> ResetEnrollmentAsync(AgentBootstrapResetRequest request, CancellationToken cancellationToken);
    Task<AgentBootstrapConnectionTestResult> TestConnectionAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken);
}
