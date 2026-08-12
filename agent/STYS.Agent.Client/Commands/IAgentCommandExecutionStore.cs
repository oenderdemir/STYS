using System.Collections.Concurrent;

namespace STYS.Agent.Client.Commands;

public interface IAgentCommandExecutionStore
{
    bool HasExecuted(string idempotencyKey);
    void MarkExecuted(string idempotencyKey);
    AgentCommandResult? GetResult(string idempotencyKey);
    void StoreResult(string idempotencyKey, AgentCommandResult result);
}

public sealed class MemoryAgentCommandExecutionStore : IAgentCommandExecutionStore
{
    private readonly ConcurrentDictionary<string, AgentCommandExecutionState> _store = new();

    public bool HasExecuted(string idempotencyKey)
    {
        var normalized = NormalizeKeyOrNull(idempotencyKey);
        return normalized is not null && _store.ContainsKey(normalized);
    }

    public void MarkExecuted(string idempotencyKey) =>
        _store.AddOrUpdate(
            NormalizeKey(idempotencyKey),
            _ => new AgentCommandExecutionState { Started = true },
            (_, existing) =>
            {
                existing.Started = true;
                return existing;
            });

    public AgentCommandResult? GetResult(string idempotencyKey)
    {
        var normalized = NormalizeKeyOrNull(idempotencyKey);
        return normalized is null ? null : _store.GetValueOrDefault(normalized)?.Result;
    }

    public void StoreResult(string idempotencyKey, AgentCommandResult result) =>
        _store.AddOrUpdate(
            NormalizeKey(idempotencyKey),
            _ => new AgentCommandExecutionState { Started = true, Result = Clone(result) },
            (_, existing) =>
            {
                existing.Started = true;
                existing.Result = Clone(result);
                return existing;
            });

    private static string NormalizeKey(string idempotencyKey) =>
        NormalizeKeyOrNull(idempotencyKey) ?? throw new InvalidOperationException("IdempotencyKey zorunludur.");

    private static string? NormalizeKeyOrNull(string? idempotencyKey)
    {
        var trimmed = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static AgentCommandResult Clone(AgentCommandResult result) => new()
    {
        Success = result.Success,
        ResultPayload = result.ResultPayload,
        ErrorCode = result.ErrorCode,
        ErrorMessage = result.ErrorMessage
    };
}
