using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentTokenService
{
    Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken);
    Task<AgentEnrollmentStatusResponse> GetEnrollmentStatusAsync(AgentEnrollmentStatusRequest request, CancellationToken cancellationToken);
    Task<AgentTokenResponse> IssueTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken);
}
