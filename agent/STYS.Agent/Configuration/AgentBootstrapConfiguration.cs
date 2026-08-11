namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapConfiguration
{
    public string StysBaseUrl { get; set; } = "https://localhost:7160";
    public int LocalUiPort { get; set; } = 5180;
    public string AgentDisplayName { get; set; } = string.Empty;
    public int HttpTimeoutSeconds { get; set; } = 30;
}
