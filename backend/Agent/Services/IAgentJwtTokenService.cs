using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Services;

public interface IAgentJwtTokenService
{
    Task<AgentTokenResponse> GenerateTokenAsync(AgentTokenDescriptor descriptor, CancellationToken cancellationToken);
}
