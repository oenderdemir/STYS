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
    private readonly AgentCommandService _commandService;
    private readonly IAgentReleaseService _releaseService;

    public AgentController(IAgentService agentService, AgentCommandService commandService, IAgentReleaseService releaseService)
    {
        _agentService = agentService;
        _commandService = commandService;
        _releaseService = releaseService;
    }

    [HttpGet]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentListDto>>> GetAll(
        [FromQuery] int? kurumId,
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken) =>
        Ok(await _agentService.GetAllAsync(kurumId, tesisId, cancellationToken));

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
        await _agentService.ApproveAsync(id, User?.Identity?.Name ?? "system", cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/reject")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> Reject(int id, CancellationToken cancellationToken)
    {
        await _agentService.RejectAsync(id, User?.Identity?.Name ?? "system", cancellationToken);
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

    /// <summary>Read-only view of the kurum enrollment policy so the enrollment dialog can render
    /// the approval choice correctly before any code has been generated. Enforcement stays in
    /// AgentTokenService; this is presentation only.</summary>
    [HttpGet("enrollment-policy")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<AgentEnrollmentPolicyDto>> GetEnrollmentPolicy(CancellationToken cancellationToken) =>
        Ok(await _agentService.GetEnrollmentPolicyAsync(cancellationToken));

    [HttpPost("enrollment-codes")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentEnrollmentCodeDto>> GenerateEnrollmentCode(
        [FromBody] AgentEnrollmentCodeRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _agentService.GenerateEnrollmentCodeAsync(request, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("enrollment-codes")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentEnrollmentCodeDto>>> GetEnrollmentCodes(
        [FromQuery] int? kurumId,
        [FromQuery] int? tesisId,
        CancellationToken cancellationToken) =>
        Ok(await _agentService.GetEnrollmentCodesAsync(kurumId, tesisId, cancellationToken));

    [HttpPost("enrollment-codes/{enrollmentId:int}/revoke")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> RevokeEnrollmentCode(int enrollmentId, CancellationToken cancellationToken)
    {
        await _agentService.RevokeEnrollmentCodeAsync(enrollmentId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/commands")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> SendCommand(int id, [FromBody] AgentCommandSendRequest request, CancellationToken cancellationToken)
    {
        request.AgentId = id;
        return Ok(await _commandService.SendAsync(request, User?.Identity?.Name ?? "system", cancellationToken));
    }

    [HttpPost("{id:int}/stage-upgrade")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> StageUpgrade(int id, CancellationToken cancellationToken) =>
        Ok(await _releaseService.StageUpgradeAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("{id:int}/apply-upgrade")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentCommandDto>> ApplyUpgrade(int id, CancellationToken cancellationToken) =>
        Ok(await _releaseService.ApplyUpgradeAsync(id, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpGet("{id:int}/commands")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentCommandDto>>> GetCommandHistory(int id, CancellationToken cancellationToken) =>
        Ok(await _commandService.GetHistoryAsync(id, cancellationToken));
}
