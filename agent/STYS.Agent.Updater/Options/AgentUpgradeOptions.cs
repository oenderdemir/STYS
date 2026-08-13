namespace STYS.Agent.Options;

public sealed class AgentUpgradeOptions
{
    public const string SectionName = "AgentUpgrade";

    public string ReleasePublicKeyPem { get; set; } = string.Empty;
}
