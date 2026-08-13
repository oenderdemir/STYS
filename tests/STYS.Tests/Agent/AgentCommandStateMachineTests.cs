using STYS.Agent.Services;
using STYS.Agent.Contracts.Enums;
using Xunit;

namespace STYS.Tests.Agent;

public sealed class AgentCommandStateMachineTests
{
    [Fact]
    public void DeliveredToFailed_IsAllowed()
    {
        Assert.True(AgentCommandStateMachine.IsValidTransition(AgentCommandStatus.Delivered, AgentCommandStatus.Failed));
        AgentCommandStateMachine.EnforceTransition(AgentCommandStatus.Delivered, AgentCommandStatus.Failed, Guid.NewGuid());
    }
}
