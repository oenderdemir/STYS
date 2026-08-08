using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;
using TOD.Platform.AspNetCore.Authorization;

namespace STYS.Agent.Controllers;

[Route("ui/agent")]
[Authorize(Policy = TOD.Platform.AspNetCore.Authorization.TodPlatformAuthorizationConstants.UiPolicy)]
public sealed class AgentController : ControllerBase
{
    private readonly IAgentService _agentService;

    public AgentController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpGet]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentListDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _agentService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<AgentDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _agentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentDto>> Create([FromBody] AgentKaydetRequest request, CancellationToken cancellationToken) =>
        Ok(await _agentService.CreateAsync(request, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentDto>> Update(int id, [FromBody] AgentKaydetRequest request, CancellationToken cancellationToken) =>
        Ok(await _agentService.UpdateAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/scopes")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> UpdateScopes(int id, [FromBody] List<string> scopes, CancellationToken cancellationToken)
    {
        await _agentService.UpdateScopesAsync(id, scopes, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/approve")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> Approve(int id, CancellationToken cancellationToken)
    {
        await _agentService.ApproveAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/disable")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> Disable(int id, CancellationToken cancellationToken)
    {
        await _agentService.DisableAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/revoke")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> Revoke(int id, CancellationToken cancellationToken)
    {
        await _agentService.RevokeAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("enrollment-codes")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentEnrollmentCodeDto>> GenerateEnrollmentCode(
        [FromBody] AgentEnrollmentCodeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _agentService.GenerateEnrollmentCodeAsync(request, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("enrollment-codes")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentEnrollmentCodeDto>>> GetEnrollmentCodes(CancellationToken cancellationToken) =>
        Ok(await _agentService.GetEnrollmentCodesAsync(cancellationToken));

    [HttpPost("enrollment-codes/{enrollmentId:int}/revoke")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> RevokeEnrollmentCode(int enrollmentId, CancellationToken cancellationToken)
    {
        await _agentService.RevokeEnrollmentCodeAsync(enrollmentId, cancellationToken);
        return Ok();
    }
}
