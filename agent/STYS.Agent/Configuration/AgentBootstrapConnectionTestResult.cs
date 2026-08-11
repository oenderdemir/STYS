namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapConnectionTestResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = "unknown";
    public string Message { get; set; } = string.Empty;
    public string? ServerTime { get; set; }
    public string? Version { get; set; }
}
