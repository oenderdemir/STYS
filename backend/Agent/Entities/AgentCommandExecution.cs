using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Agent.Entities;

public sealed class AgentCommandExecution : BaseEntity<int>, ITenantEntity
{
    public Guid CommandId { get; set; }
    public int AgentId { get; set; }
    public int KurumId { get; set; }
    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(32)]
    public string? PreviousStatus { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    [MaxLength(64)]
    public string? MachineName { get; set; }

    public AgentCommand? Command { get; set; }
}
