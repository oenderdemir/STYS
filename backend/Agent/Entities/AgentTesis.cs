using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentTesis : BaseEntity<int>, ITenantEntity
{
    public int AgentId { get; set; }
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public bool AktifMi { get; set; } = true;

    public Agent? Agent { get; set; }
}
