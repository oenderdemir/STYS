namespace STYS.Agent.Updater.Options;

public sealed class AgentUpgradeRuntimeOptions
{
    public string InstallDirectory { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int LocalUiPort { get; set; } = 5180;
    public int PollIntervalSeconds { get; set; } = 5;
    public int HealthTimeoutSeconds { get; set; } = 90;
}
