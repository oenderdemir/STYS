namespace STYS.Agent.Client;

public sealed class StysAgentClientOptions
{
    public const string SectionName = "StysAgentClient";
    public string BaseUrl { get; set; } = "https://localhost:7160";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AgentInstanceId { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string? EnrollmentCode { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
}
