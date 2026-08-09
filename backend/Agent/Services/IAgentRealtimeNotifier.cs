namespace STYS.Agent.Services;

public interface IAgentRealtimeNotifier
{
    Task AgentChangedAsync(CancellationToken cancellationToken);
}
