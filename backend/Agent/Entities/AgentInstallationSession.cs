using STYS.Agent.Contracts.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentInstallationSession : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public string AgentDisplayName { get; set; } = string.Empty;
    public string TargetRid { get; set; } = string.Empty;
    public string Scopes { get; set; } = "[]";
    public AgentInstallationSessionStatus Status { get; set; } = AgentInstallationSessionStatus.Created;
    public int? EnrolledAgentId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Agent? EnrolledAgent { get; set; }
    public AgentEnrollment? Enrollment { get; set; }
}
