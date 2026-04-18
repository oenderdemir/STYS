using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using STYS.Kamp.Dto;
using STYS.Kamp.Services;
using STYS.Licensing;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Kamp.Controllers;

[RequiresLicensedModule(StysLicensedModules.Kamp)]
public class KampBasvuruController : UIController
{
    private readonly IKampBasvuruService _kampBasvuruService;
    private readonly IKampIadeService _kampIadeService;

    public KampBasvuruController(IKampBasvuruService kampBasvuruService, IKampIadeService kampIadeService)
    {
        _kampBasvuruService = kampBasvuruService;
        _kampIadeService = kampIadeService;
    }

    [AllowAnonymous]
    [HttpGet("baglam")]
    public async Task<ActionResult<KampBasvuruBaglamDto>> GetBaglam(CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.GetBaglamAsync(cancellationToken));

    [HttpGet("benim-basvurularim")]
    [Permission(StructurePermissions.KampBasvuruYonetimi.View)]
    public async Task<ActionResult<List<KampBasvuruDto>>> GetBenimBasvurularim(CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.GetBenimBasvurularimAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KampTahsisYonetimi.View, StructurePermissions.KampRezervasyonYonetimi.View)]
    public async Task<ActionResult<KampBasvuruDto>> GetById(int id, CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.GetByIdAsync(id, cancellationToken));

    [AllowAnonymous]
    [HttpGet("basvuru-no/{basvuruNo}")]
    public async Task<ActionResult<KampBasvuruDto>> GetByBasvuruNo(string basvuruNo, CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.GetByBasvuruNoAsync(basvuruNo, cancellationToken));

    [AllowAnonymous]
    [HttpPost("onizleme")]
    public async Task<ActionResult<KampBasvuruOnizlemeDto>> Onizleme([FromBody] KampBasvuruRequestDto request, CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.OnizleAsync(request, cancellationToken));

    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<KampBasvuruDto>> BasvuruOlustur([FromBody] KampBasvuruRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _kampBasvuruService.BasvuruOlusturAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("iade-karari")]
    public ActionResult<KampIadeKarariDto> IadeKarari([FromBody] KampIadeHesaplamaRequestDto request)
        => Ok(_kampIadeService.Hesapla(request));

    [HttpDelete("{kampBasvuruId:int}/katilimci/{katilimciId:int}")]
    [Permission(StructurePermissions.KampTahsisYonetimi.Manage)]
    public async Task<ActionResult<KampKatilimciIptalSonucDto>> KatilimciIptalEt(int kampBasvuruId, int katilimciId, CancellationToken cancellationToken)
        => Ok(await _kampBasvuruService.KatilimciIptalEtAsync(kampBasvuruId, katilimciId, cancellationToken));
}
