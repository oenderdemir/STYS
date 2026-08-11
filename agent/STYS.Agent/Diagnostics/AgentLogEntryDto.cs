namespace STYS.Agent.Diagnostics;

public sealed class AgentLogEntryDto
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
