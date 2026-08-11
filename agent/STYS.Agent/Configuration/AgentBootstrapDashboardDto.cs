namespace STYS.Agent.Configuration;

public sealed class AgentBootstrapDashboardDto
{
    public string AgentDurumu { get; set; } = string.Empty;
    public string StysAdresi { get; set; } = string.Empty;
    public string EnrollmentDurumu { get; set; } = string.Empty;
    public string AgentDisplayName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string LocalUiVersion { get; set; } = string.Empty;
    public bool CredentialMevcutMu { get; set; }
    public AgentBootstrapConnectionTestResult? SonBaglantiTesti { get; set; }
    public STYS.Agent.Contracts.Dtos.AgentSelfDto? Agent { get; set; }
}
