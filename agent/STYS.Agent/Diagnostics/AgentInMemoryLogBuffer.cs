using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace STYS.Agent.Diagnostics;

public sealed class AgentInMemoryLogBuffer : IAgentLogBuffer
{
    private const int Capacity = 100;
    private static readonly Regex SecretPairRegex = new("(clientsecret|enrollmentcode|authorization|bearer|jwt|token)\\s*[:=]\\s*([^\\s,;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LongTokenRegex = new("(?<![A-Za-z0-9_\\-])[A-Za-z0-9_\\-]{24,}(?![A-Za-z0-9_\\-])", RegexOptions.Compiled);
    private readonly ConcurrentQueue<AgentLogEntryDto> _entries = new();

    public void Add(string category, string level, string message, DateTimeOffset timestampUtc)
    {
        var safeMessage = Mask(message);
        _entries.Enqueue(new AgentLogEntryDto
        {
            TimestampUtc = timestampUtc,
            Level = level,
            Category = category,
            Message = safeMessage
        });

        while (_entries.Count > Capacity && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<AgentLogEntryDto> GetRecent(int take = 100)
    {
        var count = Math.Clamp(take, 1, Capacity);
        return _entries
            .Reverse()
            .Take(count)
            .Reverse()
            .ToArray();
    }

    private static string Mask(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var masked = SecretPairRegex.Replace(input, m => $"{m.Groups[1].Value}=[REDACTED]");
        masked = LongTokenRegex.Replace(masked, "[REDACTED]");
        return masked;
    }
}
