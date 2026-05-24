using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kamp.Controllers;

public class KampRezervasyonController : UIController
{
    private readonly IKampRezervasyonService _kampRezervasyonService;
    private readonly IKampSatisBelgesiService _kampSatisBelgesiService;

    public KampRezervasyonController(
        IKampRezervasyonService kampRezervasyonService,
        IKampSatisBelgesiService kampSatisBelgesiService)
    {
        _kampRezervasyonService = kampRezervasyonService;
        _kampSatisBelgesiService = kampSatisBelgesiService;
    }

    [HttpGet("baglam")]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.View)]
    public async Task<ActionResult<KampRezervasyonBaglamDto>> GetBaglam(CancellationToken cancellationToken)
        => Ok(await _kampRezervasyonService.GetBaglamAsync(cancellationToken));

    [HttpGet]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.View)]
    public async Task<ActionResult<List<KampRezervasyonListeDto>>> GetListe([FromQuery] KampRezervasyonFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _kampRezervasyonService.GetListeAsync(filter, cancellationToken));

    [HttpPost("{kampBasvuruId:int}/uret")]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.Manage)]
    public async Task<ActionResult<KampRezervasyonUretSonucDto>> Uret(int kampBasvuruId, CancellationToken cancellationToken)
        => Ok(await _kampRezervasyonService.UretAsync(kampBasvuruId, cancellationToken));

    [HttpPost("{rezervasyonId:int}/satis-belgesi-taslagi-olustur")]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.Manage)]
    public async Task<ActionResult<SatisBelgesiDto>> OlusturSatisBelgesiTaslagi(
        [FromRoute] int rezervasyonId,
        [FromBody] KampSatisBelgesiTaslakRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _kampSatisBelgesiService.SatisBelgesiTaslagiOlusturAsync(
            rezervasyonId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}/iptal")]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.Manage)]
    public async Task<ActionResult> IptalEt(int id, [FromBody] KampRezervasyonIptalRequestDto request, CancellationToken cancellationToken)
    {
        await _kampRezervasyonService.IptalEtAsync(id, request, cancellationToken);
        return Ok();
    }
}
