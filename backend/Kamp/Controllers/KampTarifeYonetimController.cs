using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using STYS.Licensing;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Kamp.Controllers;

[Route("ui/kamptarife")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Kamp)]
public class KampTarifeYonetimController : UIController
{
    private readonly IKampTarifeYonetimService _service;

    public KampTarifeYonetimController(IKampTarifeYonetimService service)
    {
        _service = service;
    }

    [HttpGet("baglam")]
    [Permission(StructurePermissions.KampTarifeYonetimi.View)]
    public async Task<ActionResult<KampTarifeYonetimBaglamDto>> GetBaglam(CancellationToken cancellationToken = default)
    {
        var result = await _service.GetBaglamAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{kampProgramiId:int}/tarifeler")]
    [Permission(StructurePermissions.KampTarifeYonetimi.View)]
    public async Task<ActionResult<List<KampKonaklamaTarifeYonetimDto>>> GetTarifeler(int kampProgramiId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetTarifelerAsync(kampProgramiId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{kampProgramiId:int}/tarifeler")]
    [Permission(StructurePermissions.KampTarifeYonetimi.Manage)]
    public async Task<ActionResult<List<KampKonaklamaTarifeYonetimDto>>> Kaydet(
        int kampProgramiId,
        [FromBody] KampTarifeKaydetRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.KaydetAsync(kampProgramiId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("aktif")]
    [AllowAnonymous]
    public async Task<ActionResult<List<KampKonaklamaTarifeYonetimDto>>> GetAktifTarifeler(CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAktifTarifelerAsync(cancellationToken);
        return Ok(result);
    }
}
