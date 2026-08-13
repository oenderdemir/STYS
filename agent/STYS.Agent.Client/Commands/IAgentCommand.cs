namespace STYS.Agent.Client.Commands;

public sealed class AgentCommandResult
{
    public bool Success { get; set; }
    public string? ResultPayload { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public bool DeferCompletion { get; set; }

    public static AgentCommandResult Ok(string? resultPayload = null, bool deferCompletion = false) => new() { Success = true, ResultPayload = resultPayload, DeferCompletion = deferCompletion };
    public static AgentCommandResult Fail(string errorMessage, string? errorCode = null, bool deferCompletion = false) => new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode, DeferCompletion = deferCompletion };
}

public interface IAgentCommand
{
    string CommandType { get; }
}

public interface IAgentCommandHandler<in TCommand> where TCommand : IAgentCommand
{
    Task<AgentCommandResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IAgentCommandHandlerRegistry
{
    IAgentCommandHandler<TCommand>? Resolve<TCommand>(string commandType, IServiceProvider serviceProvider) where TCommand : IAgentCommand;
    IReadOnlyCollection<string> RegisteredCommandTypes { get; }
}
