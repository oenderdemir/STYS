using STYS.Agent.Client.Authentication;

namespace STYS.Agent.Services;

public sealed class AgentUnauthorizedRecoverySink : IAgentUnauthorizedRecoverySink
{
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentRuntimeStatus _runtimeStatus;

    public AgentUnauthorizedRecoverySink(
        IAgentAuthenticationState authenticationState,
        IAgentRuntimeStatus runtimeStatus)
    {
        _authenticationState = authenticationState;
        _runtimeStatus = runtimeStatus;
    }

    public void HandleAuthenticationLost()
    {
        _authenticationState.Reset();
        _runtimeStatus.ResetAuthentication();
    }
}
