namespace STYS.Agent.Client.Upgrade;

public sealed class AgentApplyUpgradeRequest
{
    public Guid CommandId { get; set; }
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public enum AgentUpgradeOutcomeStatus
{
    None = 0,
    Applying = 1,
    Applied = 2,
    RolledBack = 3,
    Failed = 4
}

public sealed class AgentUpgradeOutcome
{
    public Guid CommandId { get; set; }
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public AgentUpgradeOutcomeStatus Status { get; set; } = AgentUpgradeOutcomeStatus.None;
    public string? Message { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReportedAt { get; set; }
}

