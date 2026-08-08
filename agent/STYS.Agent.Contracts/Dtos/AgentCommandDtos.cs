namespace STYS.Agent.Contracts.Dtos;

public sealed class AgentCommandDto
{
    public Guid CommandId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AgentCommandResultRequest
{
    public Guid CommandId { get; set; }
    public bool Success { get; set; }
    public string? ResultPayload { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}
