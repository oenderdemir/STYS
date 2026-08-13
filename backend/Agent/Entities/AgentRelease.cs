using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentRelease : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ContractVersion { get; set; } = string.Empty;

    [MaxLength(50)]
    public string RuntimeIdentifier { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Sha256 { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Signature { get; set; } = string.Empty;

    public long PackageSize { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public bool Enabled { get; set; }
    public string? ReleaseNotes { get; set; }

    [MaxLength(1024)]
    public string PackagePath { get; set; } = string.Empty;
}
