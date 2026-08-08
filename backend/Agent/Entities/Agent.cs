using STYS.Agent.Contracts.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class Agent : BaseEntity<int>, ITenantEntity
{
    public string Ad { get; set; } = string.Empty;
    public string AgentKey { get; set; } = string.Empty;
    public int KurumId { get; set; }
    public AgentDurum Durum { get; set; } = AgentDurum.PendingApproval;
    public string? AgentVersion { get; set; }
    public DateTime? SonGorulmeTarihi { get; set; }
    public string? CihazKimligi { get; set; }
    public string? PublicKey { get; set; }
    public Guid? UserId { get; set; }

    public ICollection<AgentCredential> Credentialler { get; set; } = [];
    public ICollection<AgentTesis> Tesisler { get; set; } = [];
    public ICollection<AgentEnrollment> Enrollments { get; set; } = [];
}
