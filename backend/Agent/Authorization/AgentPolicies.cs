namespace STYS.Agent.Authorization;

public static class AgentPolicies
{
    public const string AgentScheme = "AgentScheme";
    public const string AgentPolicy = "AgentPolicy";

    public const string AgentHeartbeat = "agent.heartbeat";
    public const string AgentConfigRead = "agent.config.read";
    public const string AgentCommandRead = "agent.command.read";
    public const string AgentCommandExecute = "agent.command.execute";
    public const string AgentResultWrite = "agent.result.write";
}
