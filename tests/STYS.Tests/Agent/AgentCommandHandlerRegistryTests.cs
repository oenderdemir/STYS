using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;
using Xunit;

namespace STYS.Tests.Agent;

public sealed class AgentCommandHandlerRegistryTests
{
    private sealed class TestCommand : IAgentCommand
    {
        public string CommandType => "Test";
    }

    private sealed class ScopedDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class TestHandler(ScopedDependency dependency) : IAgentCommandHandler<TestCommand>
    {
        public Task<AgentCommandResult> HandleAsync(TestCommand command, CancellationToken cancellationToken)
            => Task.FromResult(AgentCommandResult.Ok(dependency.Id.ToString()));
    }

    [Fact]
    public void Resolve_UsesProvidedScopeProvider_ForScopedHandlers()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        services.AddScoped<TestHandler>();
        using var provider = services.BuildServiceProvider();

        var registry = new AgentCommandHandlerRegistry();
        registry.Register<TestCommand, TestHandler>("Test");

        using var scope = provider.CreateScope();
        var handler = registry.Resolve<TestCommand>("Test", scope.ServiceProvider);

        Assert.NotNull(handler);
    }
}
