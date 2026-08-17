namespace STYS.Agent.Client.Authentication;

public interface IAgentUnauthorizedRecoverySink
{
    void HandleAuthenticationLost();
}
