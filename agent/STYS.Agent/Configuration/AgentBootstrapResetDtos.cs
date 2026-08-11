namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapResetRequest
{
    public string ConfirmationText { get; set; } = string.Empty;
}

public sealed class AgentBootstrapResetResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool CredentialCleared { get; set; }
    public bool TokenCleared { get; set; }
    public bool AuthenticationReset { get; set; }
}
