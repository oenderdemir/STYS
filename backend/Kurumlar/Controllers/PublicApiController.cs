using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Services;

namespace STYS.Kurumlar.Controllers;

[ApiController]
[Route("public")]
public class PublicApiController : ControllerBase
{
    private readonly ITenantBrandingService _tenantBrandingService;

    public PublicApiController(ITenantBrandingService tenantBrandingService)
    {
        _tenantBrandingService = tenantBrandingService;
    }

    [HttpGet("tenant-branding")]
    [AllowAnonymous]
    public async Task<ActionResult<TenantBrandingDto>> GetTenantBranding(
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        var branding = await _tenantBrandingService.GetBrandingAsync(host, Request.Host.Host, cancellationToken);
        return Ok(branding);
    }
}
