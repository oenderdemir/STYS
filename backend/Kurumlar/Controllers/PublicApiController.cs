using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Kurumlar.Dto;
using STYS.Kurumlar.Services;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kurumlar.Controllers;

[Route("api/public")]
public class PublicApiController : UIController
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
