using STYS.Agent.Contracts.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentEnrollment : BaseEntity<int>, ITenantEntity
{
    public string Code { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public string TesisIds { get; set; } = "[]";
    public string AllowedScopes { get; set; } = "[]";
    public int KullanimSayisi { get; set; }
    public int MaxKullanimSayisi { get; set; } = 1;
    public DateTime ExpiresAt { get; set; }
    public AgentEnrollmentDurum Durum { get; set; } = AgentEnrollmentDurum.Active;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public int? AgentId { get; set; }

    public Agent? Agent { get; set; }
}
