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

    public bool HasExecuted(string idempotencyKey) => _store.ContainsKey(NormalizeKey(idempotencyKey));

    public void MarkExecuted(string idempotencyKey) =>
        _store.AddOrUpdate(
            NormalizeKey(idempotencyKey),
            _ => new AgentCommandExecutionState { Started = true },
            (_, existing) =>
            {
                existing.Started = true;
                return existing;
            });

    public AgentCommandResult? GetResult(string idempotencyKey) => _store.GetValueOrDefault(NormalizeKey(idempotencyKey))?.Result;

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
        string.IsNullOrWhiteSpace(idempotencyKey) ? throw new InvalidOperationException("IdempotencyKey zorunludur.") : idempotencyKey.Trim();

    private static AgentCommandResult Clone(AgentCommandResult result) => new()
    {
        Success = result.Success,
        ResultPayload = result.ResultPayload,
        ErrorCode = result.ErrorCode,
        ErrorMessage = result.ErrorMessage
    };
}
