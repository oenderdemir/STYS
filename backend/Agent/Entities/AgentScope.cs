using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentScope : BaseEntity<int>, ITenantEntity
{
    public int AgentId { get; set; }
    public int KurumId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public bool AktifMi { get; set; } = true;

    public Agent? Agent { get; set; }
}
