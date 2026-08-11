namespace STYS.Agent.Services;

public interface IAgentAuthenticationState
{
    bool IsReady { get; }
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    void MarkAuthenticated();
    void Reset();
}

public sealed class AgentAuthenticationState : IAgentAuthenticationState
{
    private readonly object _gate = new();
    private TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _ready.Task.IsCompletedSuccessfully;
            }
        }
    }

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_ready.Task.IsCompletedSuccessfully)
                return Task.CompletedTask;

            return _ready.Task.WaitAsync(cancellationToken);
        }
    }

    public void MarkAuthenticated()
    {
        lock (_gate)
        {
            _ready.TrySetResult();
        }
    }

    public void Reset()
    {
        TaskCompletionSource previous;
        lock (_gate)
        {
            previous = _ready;
            _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        previous.TrySetResult();
    }
}
