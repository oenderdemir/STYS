using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;
using TOD.Platform.AspNetCore.Authorization;

namespace STYS.Agent.Controllers;

[Route("api/ui/agent-installations")]
[ApiController]
[Authorize(Policy = TOD.Platform.AspNetCore.Authorization.TodPlatformAuthorizationConstants.UiPolicy)]
public sealed class AgentInstallationSessionsController : ControllerBase
{
    private readonly IAgentInstallationSessionService _service;

    public AgentInstallationSessionsController(IAgentInstallationSessionService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentInstallationSessionDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<AgentInstallationSessionDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentInstallationSessionCreateResponse>> Create(
        [FromBody] AgentInstallationSessionCreateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.CreateAsync(request, User?.Identity?.Name ?? "system", cancellationToken));

    [HttpPost("{id:int}/cancel")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, User?.Identity?.Name ?? "system", cancellationToken);
        return Ok();
    }

    [HttpGet("{id:int}/package")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult> DownloadPackage(int id, CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var package = await _service.GetPackageAsync(id, baseUrl, cancellationToken);
        return File(package.Content, package.ContentType, package.FileName);
    }
}
