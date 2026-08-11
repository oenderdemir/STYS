namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapEnrollmentRequest
{
    public string StysBaseUrl { get; set; } = "https://localhost:7160";
    public string AgentDisplayName { get; set; } = string.Empty;
    public string EnrollmentCode { get; set; } = string.Empty;
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int LocalUiPort { get; set; } = 5180;
    public IReadOnlyCollection<string> Capabilities { get; set; } = [];
}

public sealed class AgentBootstrapEnrollmentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AgentId { get; set; }
    public string AgentDisplayName { get; set; } = string.Empty;
    public bool CredentialSaved { get; set; }
    public bool TokenAcquired { get; set; }
    public bool RestartRequired { get; set; }
}
