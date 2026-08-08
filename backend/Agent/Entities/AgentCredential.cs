using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentCredential : BaseEntity<int>, ITenantEntity
{
    public int AgentId { get; set; }
    public int KurumId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;
    public int CredentialVersion { get; set; } = 1;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public Agent? Agent { get; set; }
}
