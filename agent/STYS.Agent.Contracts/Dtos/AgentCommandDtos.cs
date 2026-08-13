namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentCommandDto
{
    public Guid Id { get; set; }
    public int AgentId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public int Status { get; set; }
    public int Priority { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ResultPayload { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AgentCommandLeaseRequest
{
    public string LeaseToken { get; set; } = string.Empty;
}

public sealed class AgentCommandAcceptRequest : AgentCommandLeaseRequest
{
}

public sealed class AgentCommandCompleteRequest : AgentCommandLeaseRequest
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? ResultPayload { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class AgentCommandRenewRequest : AgentCommandLeaseRequest
{
}

public sealed class AgentCommandSendRequest
{
    public int AgentId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public int Priority { get; set; }
    public int? ExpirationMinutes { get; set; }
    public int MaxRetryCount { get; set; } = 3;
}

public sealed class AgentApplyUpgradeRequest
{
    public Guid CommandId { get; set; }
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public sealed class AgentApplyUpgradeResponse
{
    public Guid CommandId { get; set; }
    public int ReleaseId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public string ApplyStatus { get; set; } = string.Empty;
    public string? Message { get; set; }
}
