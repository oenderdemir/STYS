using STYS.Agent.Configuration;

namespace STYS.Agent.Services;

public interface IAgentEnrollmentCoordinator
{
    Task<AgentBootstrapEnrollmentResult> EnrollAsync(AgentBootstrapEnrollmentRequest request, CancellationToken cancellationToken);
    Task<bool> TryActivateAsync(CancellationToken cancellationToken);
}
