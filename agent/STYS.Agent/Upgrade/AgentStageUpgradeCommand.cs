using STYS.Agent.Client.Commands;
using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Upgrade;

public sealed class AgentStageUpgradeCommand : IAgentCommand
{
    public string CommandType => "AgentStageUpgrade";

    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public long PackageSize { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string? ReleaseNotes { get; set; }

    public AgentStageUpgradeRequest ToRequest() => new()
    {
        ReleaseId = ReleaseId,
        Version = Version,
        ContractVersion = ContractVersion,
        RuntimeIdentifier = RuntimeIdentifier,
        Sha256 = Sha256,
        Signature = Signature,
        PackageSize = PackageSize,
        PublishedAt = PublishedAt,
        ReleaseNotes = ReleaseNotes
    };
}
