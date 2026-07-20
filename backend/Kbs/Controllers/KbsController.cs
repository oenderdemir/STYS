using Microsoft.AspNetCore.Mvc;
using STYS.Kbs.Constants;
using STYS.Kbs.Dtos;
using STYS.Kbs.Services;
using STYS.Licensing;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Kbs.Controllers;

[RequiresLicensedModule(StysLicensedModules.Kbs)]
public class KbsController(IKbsYonetimService yonetim, IKbsBildirimOlusturmaService olaylar) : UIController
{
    [HttpGet("tesisler/{tesisId:int}/ayar")]
    [Permission(StructurePermissions.KbsYonetimi.View)]
    public async Task<ActionResult<KbsTesisAyariDto?>> GetAyar(int tesisId, CancellationToken ct) => Ok(await yonetim.GetAyarAsync(tesisId, ct));

    [HttpPut("tesisler/{tesisId:int}/ayar")]
    [Permission(StructurePermissions.KbsYonetimi.Settings)]
    public async Task<ActionResult<KbsTesisAyariDto>> UpdateAyar(int tesisId, [FromBody] KbsTesisAyariGuncelleDto request, CancellationToken ct) => Ok(await yonetim.UpdateAyarAsync(tesisId, request, ct));

    [HttpPost("tesisler/{tesisId:int}/baglanti-kontrol")]
    [Permission(StructurePermissions.KbsYonetimi.Settings)]
    public async Task<ActionResult<KbsBaglantiTestSonucu>> BaglantiKontrol(int tesisId, CancellationToken ct) => Ok(await yonetim.BaglantiKontrolAsync(tesisId, ct));

    [HttpGet("ozet")]
    [Permission(StructurePermissions.KbsYonetimi.View)]
    public async Task<ActionResult<KbsGunlukOzetDto>> Ozet([FromQuery] int? tesisId, CancellationToken ct) => Ok(await yonetim.GunlukOzetAsync(tesisId, ct));

    [HttpGet("bildirimler")]
    [Permission(StructurePermissions.KbsYonetimi.View)]
    public async Task<ActionResult<KbsSayfaliSonucDto<KbsBildirimListeDto>>> Listele([FromQuery] int? tesisId, [FromQuery] string? durum, [FromQuery] string? bildirimTipi, [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 25, CancellationToken ct = default) => Ok(await yonetim.ListeleAsync(tesisId, durum, bildirimTipi, sayfa, sayfaBoyutu, false, ct));

    [HttpGet("bildirimler/hassas")]
    [Permission(StructurePermissions.KbsYonetimi.SensitiveDataView)]
    public async Task<ActionResult<KbsSayfaliSonucDto<KbsBildirimListeDto>>> ListeleHassas([FromQuery] int? tesisId, [FromQuery] string? durum, [FromQuery] string? bildirimTipi, [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 25, CancellationToken ct = default) => Ok(await yonetim.ListeleAsync(tesisId, durum, bildirimTipi, sayfa, sayfaBoyutu, true, ct));

    [HttpPost("bildirimler/{bildirimId:long}/tekrar-dene")]
    [Permission(StructurePermissions.KbsYonetimi.Retry)]
    public async Task<IActionResult> TekrarDene(long bildirimId, CancellationToken ct) { await yonetim.TekrarKuyrugaAlAsync(bildirimId, ct); return NoContent(); }

    [HttpPost("bildirimler/{bildirimId:long}/manuel-mudahale")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<IActionResult> ManuelMudahale(long bildirimId, CancellationToken ct) { await yonetim.ManuelMudahaleAsync(bildirimId, ct); return NoContent(); }

    [HttpGet("tesisler/{tesisId:int}/egm-excel/{bildirimTipi}")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<IActionResult> EgmExcel(int tesisId, string bildirimTipi, CancellationToken ct)
    {
        if (bildirimTipi is not (KbsBildirimTipleri.Giris or KbsBildirimTipleri.Cikis)) return BadRequest("Yalnizca giris veya cikis Excel'i olusturulabilir.");
        var result = await yonetim.EgmExcelOlusturAsync(tesisId, bildirimTipi, ct);
        Response.Headers.Append("X-Kbs-Manifest-Hash", result.ManifestHash);
        return File(result.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.FileName);
    }

    [HttpPost("tesisler/{tesisId:int}/egm-yukleme-onayi/{manifestHash}")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<IActionResult> EgmYuklemeOnayi(int tesisId, string manifestHash, CancellationToken ct) { await yonetim.EgmYuklemeOnaylaAsync(tesisId, manifestHash, ct); return NoContent(); }

    [HttpPost("konaklayanlar/{konaklayanId:int}/fiili-giris")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<ActionResult<KbsFiiliOlaySonucDto>> FiiliGiris(int konaklayanId, CancellationToken ct) => Ok(await olaylar.FiiliGirisYapAsync(konaklayanId, null, ct));

    [HttpPost("konaklayanlar/{konaklayanId:int}/fiili-cikis")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<ActionResult<KbsFiiliOlaySonucDto>> FiiliCikis(int konaklayanId, CancellationToken ct) => Ok(await olaylar.FiiliCikisYapAsync(konaklayanId, null, ct));

    [HttpPost("konaklayanlar/{konaklayanId:int}/oda-degisikligi")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<ActionResult<KbsFiiliOlaySonucDto>> OdaDegisikligi(int konaklayanId, [FromBody] KbsOdaDegisikligiRequestDto request, CancellationToken ct) => Ok(await olaylar.OdaDegisikligiBildirAsync(konaklayanId, request.OdaNo, request.OlayTarihi, ct));

    [HttpPost("konaklayanlar/{konaklayanId:int}/gelmeyecek")]
    [Permission(StructurePermissions.KbsYonetimi.Manage)]
    public async Task<ActionResult<KbsFiiliOlaySonucDto>> Gelmeyecek(int konaklayanId, CancellationToken ct) => Ok(await olaylar.GelmeyecekOlarakIsaretleAsync(konaklayanId, ct));
}
