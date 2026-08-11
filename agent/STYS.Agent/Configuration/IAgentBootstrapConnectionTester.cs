namespace STYS.Agent.Configuration;

public interface IAgentBootstrapConnectionTester
{
    Task<AgentBootstrapConnectionTestResult> TestAsync(string baseUrl, int timeoutSeconds, CancellationToken cancellationToken);
}
