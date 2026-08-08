namespace STYS.Agent.Services;

public sealed class AgentAuthOptions
{
    public const string SectionName = "Agent";
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int HeartbeatTimeoutMinutes { get; set; } = 5;
}
