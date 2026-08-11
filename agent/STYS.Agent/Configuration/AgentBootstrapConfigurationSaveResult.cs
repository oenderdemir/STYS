namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapConfigurationSaveResult
{
    public AgentBootstrapConfiguration Configuration { get; set; } = new();
    public bool RestartRequired { get; set; }
    public bool ReEnrollmentRequired { get; set; }
    public string? Message { get; set; }
}
