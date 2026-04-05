using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kamp.Controllers;

public class KampTahsisController : UIController
{
    private readonly IKampTahsisService _kampTahsisService;

    public KampTahsisController(IKampTahsisService kampTahsisService)
    {
        _kampTahsisService = kampTahsisService;
    }

    [HttpGet("baglam")]
    [Permission(StructurePermissions.KampTahsisYonetimi.View)]
    public async Task<ActionResult<KampTahsisBaglamDto>> GetBaglam(CancellationToken cancellationToken)
        => Ok(await _kampTahsisService.GetBaglamAsync(cancellationToken));

    [HttpGet]
    [Permission(StructurePermissions.KampTahsisYonetimi.View)]
    public async Task<ActionResult<List<KampTahsisListeDto>>> GetListe([FromQuery] KampTahsisFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _kampTahsisService.GetListeAsync(filter, cancellationToken));

    [HttpPut("{kampBasvuruId:int}/karar")]
    [Permission(StructurePermissions.KampTahsisYonetimi.Manage)]
    public async Task<ActionResult> KararVer(int kampBasvuruId, [FromBody] KampTahsisKararRequestDto request, CancellationToken cancellationToken)
    {
        await _kampTahsisService.KararVerAsync(kampBasvuruId, request, cancellationToken);
        return Ok();
    }

    [HttpPost("otomatik-karar")]
    [Permission(StructurePermissions.KampTahsisYonetimi.Manage)]
    public async Task<ActionResult<KampTahsisOtomatikKararSonucDto>> OtomatikKararUygula(
        [FromBody] KampTahsisOtomatikKararRequestDto request,
        CancellationToken cancellationToken)
        => Ok(await _kampTahsisService.OtomatikKararUygulaAsync(request, cancellationToken));

    [HttpPost("{kampDonemiId:int}/noshow-iptal")]
    [Permission(StructurePermissions.KampTahsisYonetimi.Manage)]
    public async Task<ActionResult<KampNoShowIptalSonucDto>> NoShowIptalUygula(int kampDonemiId, CancellationToken cancellationToken)
        => Ok(await _kampTahsisService.NoShowIptalUygulaAsync(kampDonemiId, cancellationToken));
}
