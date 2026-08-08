using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using STYS.Agent.Authorization;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Entities;
using STYS.Agent.Services;
using STYS.Infrastructure.EntityFramework;
using AgentEntity = STYS.Agent.Entities.Agent;

namespace STYS.Agent.Controllers;

[Route("api/agent")]
[ApiController]
public sealed class AgentAuthController : ControllerBase
{
    private readonly IAgentTokenService _tokenService;
    private readonly IDbContextFactory<StysAppDbContext> _dbContextFactory;
    private readonly AgentCommandService _commandService;

    public AgentAuthController(
        IAgentTokenService tokenService,
        IDbContextFactory<StysAppDbContext> dbContextFactory,
        AgentCommandService commandService)
    {
        _tokenService = tokenService;
        _dbContextFactory = dbContextFactory;
        _commandService = commandService;
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
    [Authorize(Policy = AgentPolicies.AgentHeartbeat)]
    public async Task<ActionResult<AgentHeartbeatResponse>> Heartbeat(
        [FromBody] AgentHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated)
            return Unauthorized();

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await db.Set<AgentEntity>()
            .FirstOrDefaultAsync(x => x.Id == agentContext.AgentId && !x.IsDeleted, cancellationToken);

        if (agent is not null)
        {
            agent.LastHeartbeatAt = DateTime.UtcNow;
            agent.SonGorulmeTarihi = DateTime.UtcNow;
            agent.AgentVersion = request.AgentVersion;
            agent.CihazKimligi ??= request.CihazKimligi;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new AgentHeartbeatResponse
        {
            RequiredUpdate = false
        });
    }

    [HttpGet("config")]
    [Authorize(Policy = AgentPolicies.AgentConfigRead)]
    public async Task<ActionResult<AgentConfigDto>> GetConfig(
        [FromQuery] long currentVersion,
        CancellationToken cancellationToken)
    {
        return Ok(new AgentConfigDto
        {
            Version = 1,
            Configs = new Dictionary<string, string>
            {
                ["heartbeatIntervalSeconds"] = "30",
                ["commandPollIntervalSeconds"] = "10",
                ["maxRetryCount"] = "3"
            }
        });
    }

    [HttpGet("commands")]
    [Authorize(Policy = AgentPolicies.AgentCommandRead)]
    public async Task<ActionResult<IReadOnlyCollection<AgentCommandDto>>> GetPendingCommands(CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        return Ok(await _commandService.GetPendingCommandsAsync(agentContext.AgentId, cancellationToken));
    }

    [HttpPost("commands/{id:guid}/accept")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> AcceptCommand(Guid id, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.AcceptAsync(id, agentContext.AgentId, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/running")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> SetRunningCommand(Guid id, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.SetRunningAsync(id, agentContext.AgentId, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/complete")]
    [Authorize(Policy = AgentPolicies.AgentResultWrite)]
    public async Task<ActionResult> CompleteCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.CompleteAsync(id, agentContext.AgentId, request, cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/fail")]
    [Authorize(Policy = AgentPolicies.AgentResultWrite)]
    public async Task<ActionResult> FailCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.FailAsync(id, agentContext.AgentId, request.ErrorMessage ?? "Unknown error", cancellationToken);
        return Ok();
    }

    [HttpPost("commands/{id:guid}/reject")]
    [Authorize(Policy = AgentPolicies.AgentCommandExecute)]
    public async Task<ActionResult> RejectCommand(Guid id, [FromBody] AgentCommandCompleteRequest request, CancellationToken cancellationToken)
    {
        var agentContext = HttpContext.RequestServices.GetRequiredService<ICurrentAgentContext>();
        if (!agentContext.IsAuthenticated) return Unauthorized();
        await _commandService.RejectAsync(id, agentContext.AgentId, request.ErrorMessage ?? "Unknown command", cancellationToken);
        return Ok();
    }
}
