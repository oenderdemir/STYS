using STYS.Agent.Contracts.Enums;

namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentInstallationSessionCreateRequest
{
    public int TesisId { get; set; }
    public string AgentDisplayName { get; set; } = string.Empty;
    public string TargetRid { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public bool RequiresApproval { get; set; }
    public int? ExpirationHours { get; set; }
}

public sealed class AgentInstallationSessionCreateResponse
{
    public AgentInstallationSessionDto Session { get; set; } = new();
    public string EnrollmentCode { get; set; } = string.Empty;
}

public sealed class AgentInstallationSessionDto
{
    public int Id { get; set; }
    public int KurumId { get; set; }
    public int TesisId { get; set; }
    public string? TesisAd { get; set; }
    public string AgentDisplayName { get; set; } = string.Empty;
    public string TargetRid { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Scopes { get; set; } = [];
    public AgentInstallationSessionStatus Status { get; set; }
    public int? EnrollmentId { get; set; }
    public int? EnrolledAgentId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
