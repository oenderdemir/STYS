using System.ComponentModel.DataAnnotations;
using STYS.Agent.Contracts.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentCommand : BaseEntity<Guid>, ITenantEntity
{
    public int AgentId { get; set; }
    public int KurumId { get; set; }
    [MaxLength(128)]
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public AgentCommandStatus Status { get; set; } = AgentCommandStatus.Pending;
    public int Priority { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; } = 3;
    [MaxLength(64)]
    public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(128)]
    public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(256)]
    public string? RequestedBy { get; set; }
    public string? ResultPayload { get; set; }
    [MaxLength(128)]
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public Agent? Agent { get; set; }
    public ICollection<AgentCommandExecution> Executions { get; set; } = [];
}
