using Microsoft.Extensions.DependencyInjection;

namespace STYS.Agent.Client.Commands;

public sealed class AgentCommandHandlerRegistry : IAgentCommandHandlerRegistry
{
    private readonly Dictionary<string, Type> _handlerTypes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> RegisteredCommandTypes => _handlerTypes.Keys.ToList();

    public void Register<TCommand, THandler>(string commandType)
        where TCommand : IAgentCommand
        where THandler : IAgentCommandHandler<TCommand>
    {
        _handlerTypes[commandType] = typeof(THandler);
    }

    public IAgentCommandHandler<TCommand>? Resolve<TCommand>(string commandType, IServiceProvider serviceProvider) where TCommand : IAgentCommand
    {
        if (!_handlerTypes.TryGetValue(commandType, out var handlerType))
            return null;

        return (IAgentCommandHandler<TCommand>?)serviceProvider.GetService(handlerType);
    }
}
