using STYS.Agent.Contracts.Enums;

namespace STYS.Agent.Upgrade;

public sealed class AgentReleaseStagingState
{
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public AgentReleaseStageStatus StageStatus { get; set; } = AgentReleaseStageStatus.None;
    public string? Message { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public long PackageSize { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? PackagePath { get; set; }
    public DateTimeOffset? DownloadingAt { get; set; }
    public DateTimeOffset? VerifyingAt { get; set; }
    public DateTimeOffset? StagedAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
