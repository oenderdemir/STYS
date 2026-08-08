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
    private readonly Dictionary<string, AgentCommandResult> _store = new();

    public bool HasExecuted(string idempotencyKey) => _store.ContainsKey(idempotencyKey);

    public void MarkExecuted(string idempotencyKey) => _store[idempotencyKey] = AgentCommandResult.Ok();

    public AgentCommandResult? GetResult(string idempotencyKey) => _store.GetValueOrDefault(idempotencyKey);

    public void StoreResult(string idempotencyKey, AgentCommandResult result) => _store[idempotencyKey] = result;
}
