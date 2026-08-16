using STYS.Agent.Contracts.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentEnrollment : BaseEntity<int>, ITenantEntity
{
    /// <summary>SHA-256 of the enrollment code. The plaintext code is returned to the operator
    /// exactly once, at generation time, and is never persisted or recoverable afterwards.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>First few characters of the code, stored so operators can tell codes apart in the
    /// UI. Not a secret on its own and never sufficient to enroll.</summary>
    public string CodePrefix { get; set; } = string.Empty;

    public int KurumId { get; set; }
    public string TesisIds { get; set; } = "[]";
    public string AllowedScopes { get; set; } = "[]";
    public int KullanimSayisi { get; set; }
    public int MaxKullanimSayisi { get; set; } = 1;
    public DateTime ExpiresAt { get; set; }
    public bool RequiresApproval { get; set; }
    public AgentEnrollmentDurum Durum { get; set; } = AgentEnrollmentDurum.Active;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public int? AgentId { get; set; }
    public int? AgentInstallationSessionId { get; set; }

    public Agent? Agent { get; set; }
    public AgentInstallationSession? InstallationSession { get; set; }
}
