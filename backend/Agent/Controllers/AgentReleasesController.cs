using Microsoft.AspNetCore.Mvc;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Agent.Controllers;

// Overrides UIController's default "ui/[controller]" so the path stays kebab-cased. No "api/"
// segment: the reverse proxy strips one before the request reaches MVC.
[Route("ui/agent-releases")]
public sealed class AgentReleasesController : UIController
{
    private readonly IAgentReleasePublishingService _service;

    public AgentReleasesController(IAgentReleasePublishingService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<IReadOnlyCollection<AgentReleaseDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.AgentYonetimi.View)]
    public async Task<ActionResult<AgentReleaseDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Publishes a new release. Sha256 and PackageSize are computed from the uploaded bytes; any
    /// values a client might send for them are ignored by design.
    /// </summary>
    [HttpPost]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    [RequestSizeLimit(512L * 1024 * 1024)]
    public async Task<ActionResult<AgentReleaseDto>> Publish(
        [FromForm] AgentReleasePublishRequest request,
        IFormFile package,
        CancellationToken cancellationToken)
    {
        if (package is null || package.Length == 0)
        {
            throw new BaseException("Release paketi gönderilmedi.", 400);
        }

        await using var stream = package.OpenReadStream();
        return Ok(await _service.PublishAsync(request, stream, cancellationToken));
    }

    [HttpPost("{id:int}/enable")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentReleaseDto>> Enable(int id, CancellationToken cancellationToken) =>
        Ok(await _service.SetEnabledAsync(id, enabled: true, cancellationToken));

    [HttpPost("{id:int}/disable")]
    [Permission(StructurePermissions.AgentYonetimi.Manage)]
    public async Task<ActionResult<AgentReleaseDto>> Disable(int id, CancellationToken cancellationToken) =>
        Ok(await _service.SetEnabledAsync(id, enabled: false, cancellationToken));
}
