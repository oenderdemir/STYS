namespace STYS.Agent.Authorization;

public static class AgentPolicies
{
    public const string AgentScheme = "AgentScheme";
    public const string AgentPolicy = "AgentPolicy";

    public const string AgentHeartbeat = "Agent.Heartbeat";
    public const string AgentConfigRead = "Agent.ConfigRead";
    public const string AgentCommandRead = "Agent.CommandRead";
    public const string AgentCommandExecute = "Agent.CommandExecute";
    public const string AgentResultWrite = "Agent.ResultWrite";
}
