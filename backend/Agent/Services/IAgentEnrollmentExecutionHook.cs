using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Services;

public interface IAgentEnrollmentExecutionHook
{
    Task AfterEntitiesCreatedBeforeCommitAsync(AgentEntity agent, CancellationToken cancellationToken);
}

public sealed class NoOpAgentEnrollmentExecutionHook : IAgentEnrollmentExecutionHook
{
    public Task AfterEntitiesCreatedBeforeCommitAsync(AgentEntity agent, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
