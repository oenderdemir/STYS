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
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class AgentCommandAcceptRequest
{
    public Guid Id { get; set; }
}

public sealed class AgentCommandCompleteRequest
{
    public Guid Id { get; set; }
    public bool Success { get; set; }
    public string? ResultPayload { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
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
