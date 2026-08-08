using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;

namespace STYS.Agent.Controllers;

[Route("api/agent")]
[ApiController]
public sealed class AgentAuthController : ControllerBase
{
    private readonly IAgentTokenService _tokenService;

    public AgentAuthController(IAgentTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpPost("enroll")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentEnrollmentResponse>> Enroll(
        [FromBody] AgentEnrollmentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _tokenService.EnrollAsync(request, cancellationToken));

    [HttpPost("auth/token")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentTokenResponse>> GetToken(
        [FromBody] AgentTokenRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _tokenService.IssueTokenAsync(request, cancellationToken));

    [HttpPost("heartbeat")]
    [Authorize(Policy = AgentPolicies.AgentPolicy)]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat(
        [FromBody] AgentHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var response = new AgentHeartbeatResponse
        {
            MinimumSupportedAgentVersion = "1.0.0",
            RequiredContractVersion = "1.0.0",
            RequiredUpdate = false
        };
        return Ok(response);
    }

    [HttpGet("config")]
    [Authorize(Policy = AgentPolicies.AgentPolicy)]
    public async Task<ActionResult<AgentConfigDto>> GetConfig(
        [FromQuery] long currentVersion,
        CancellationToken cancellationToken)
    {
        var config = new AgentConfigDto
        {
            Version = 1,
            Configs = new Dictionary<string, string>
            {
                ["heartbeatIntervalSeconds"] = "30",
                ["commandPollIntervalSeconds"] = "10",
                ["maxRetryCount"] = "3"
            }
        };
        return Ok(config);
    }
}
