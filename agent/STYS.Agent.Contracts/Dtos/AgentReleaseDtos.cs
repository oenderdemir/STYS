using STYS.Agent.Contracts.Enums;

namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentReleaseDto
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public long PackageSize { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool Enabled { get; set; }
    public string? ReleaseNotes { get; set; }
}

public sealed class AgentStageUpgradeRequest
{
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public long PackageSize { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? ReleaseNotes { get; set; }
}

public sealed class AgentStageUpgradeResponse
{
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public AgentReleaseStageStatus StageStatus { get; set; } = AgentReleaseStageStatus.None;
    public string? Message { get; set; }
}
