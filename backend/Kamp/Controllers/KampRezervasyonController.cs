using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Kamp.Controllers;

public class KampRezervasyonController : UIController
{
    private readonly IKampRezervasyonService _kampRezervasyonService;

    public KampRezervasyonController(IKampRezervasyonService kampRezervasyonService)
    {
        _kampRezervasyonService = kampRezervasyonService;
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

    [HttpPut("{id:int}/iptal")]
    [Permission(StructurePermissions.KampRezervasyonYonetimi.Manage)]
    public async Task<ActionResult> IptalEt(int id, [FromBody] KampRezervasyonIptalRequestDto request, CancellationToken cancellationToken)
    {
        await _kampRezervasyonService.IptalEtAsync(id, request, cancellationToken);
        return Ok();
    }
}
