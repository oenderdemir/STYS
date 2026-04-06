using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kamp.Controllers;

public class KampPuanKuraliController : UIController
{
    private readonly IKampPuanKuraliYonetimService _kampPuanKuraliYonetimService;

    public KampPuanKuraliController(IKampPuanKuraliYonetimService kampPuanKuraliYonetimService)
    {
        _kampPuanKuraliYonetimService = kampPuanKuraliYonetimService;
    }

    [HttpGet("yonetim-baglam")]
    [Permission(
        StructurePermissions.KampPuanKuraliYonetimi.View)]
    public async Task<ActionResult<KampPuanKuraliYonetimBaglamDto>> GetYonetimBaglam(CancellationToken cancellationToken)
    {
        var result = await _kampPuanKuraliYonetimService.GetBaglamAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("yonetim-baglam")]
    [Permission(
        StructurePermissions.KampPuanKuraliYonetimi.Manage)]
    public async Task<ActionResult<KampPuanKuraliYonetimBaglamDto>> KaydetYonetimBaglam(
        [FromBody] KampPuanKuraliYonetimKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _kampPuanKuraliYonetimService.KaydetAsync(request, cancellationToken);
        return Ok(result);
    }
}
