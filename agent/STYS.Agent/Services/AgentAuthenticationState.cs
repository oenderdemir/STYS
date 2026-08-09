namespace STYS.Agent.Services;

public interface IAgentAuthenticationState
{
    bool IsReady { get; }
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    void MarkAuthenticated();
}

public sealed class AgentAuthenticationState : IAgentAuthenticationState
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady => _ready.Task.IsCompletedSuccessfully;

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        if (IsReady)
            return Task.CompletedTask;

        return _ready.Task.WaitAsync(cancellationToken);
    }

    public void MarkAuthenticated()
    {
        _ready.TrySetResult();
    }
}
