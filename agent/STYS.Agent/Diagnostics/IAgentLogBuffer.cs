namespace STYS.Agent.Diagnostics;

public interface IAgentLogBuffer
{
    void Add(string category, string level, string message, DateTimeOffset timestampUtc);
    IReadOnlyCollection<AgentLogEntryDto> GetRecent(int take = 100);
}
