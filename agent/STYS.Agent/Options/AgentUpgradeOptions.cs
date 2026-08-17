namespace STYS.Agent.Options;

public sealed class AgentUpgradeOptions
{
    public const string SectionName = "AgentUpgrade";

    public string ReleasePublicKeyPem { get; set; } = string.Empty;
    public string ReleasePublicKeyPemPath { get; set; } = string.Empty;

    /// <summary>
    /// Windows service that actually performs the upgrade. The agent only writes an apply request
    /// and waits, so without this service the command would defer forever; the name is
    /// configurable for installs that register it differently.
    /// </summary>
    public string UpdaterServiceName { get; set; } = "STYS Agent Updater";
}
