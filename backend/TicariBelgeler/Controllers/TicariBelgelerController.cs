using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using STYS.TicariBelgeler.Dtos;
using STYS.TicariBelgeler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.TicariBelgeler.Controllers;

/// <summary>
/// Operasyon modülleri (resepsiyon, rezervasyon, restoran, kamp vb.) için TicariBelge API sınırı.
/// Muhasebe onayı, ret, fiş oluşturma ve fatura kesme endpointleri BİLİNÇLİ OLARAK burada
/// BULUNMAZ (bkz. görev D/F) - bunlar yalnızca ui/muhasebe/satis-belgeleri üzerinden yapılabilir.
/// Ayrı, muhasebe satış belgesi yetkisinden BAĞIMSIZ TicariBelgeYonetimi.View/Manage yetkilerini kullanır.
/// </summary>
[Route("ui/ticari-belgeler")]
public class TicariBelgelerController : UIController
{
    private readonly ITicariBelgeService _service;
    private readonly ITicariBelgeLookupService _lookupService;

    public TicariBelgelerController(ITicariBelgeService service, ITicariBelgeLookupService lookupService)
    {
        _service = service;
        _lookupService = lookupService;
    }

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("filter")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> Filter(
        [FromBody] TicariBelgeFilterDto filter,
        CancellationToken cancellationToken)
    {
        var result = await _service.FilterAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kaynaktan-taslak-olustur")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.Manage)]
    public async Task<IActionResult> KaynaktanTaslakOlustur(
        [FromBody] TicariBelgeTaslakOlusturRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.KaynaktanTaslakOlusturAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.Manage)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] TicariBelgeGuncelleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.Manage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/muhasebe-onayina-gonder")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.Manage)]
    public async Task<IActionResult> MuhasebeOnayinaGonder(int id, CancellationToken cancellationToken)
    {
        await _service.MuhasebeOnayinaGonderAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/iptal")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.Manage)]
    public async Task<IActionResult> IptalEt(int id, CancellationToken cancellationToken)
    {
        await _service.IptalEtAsync(id, cancellationToken);
        return Ok();
    }

    // ──────────────────────────────────────────────
    //  Operasyonel lookup sınırı (bkz. görev A) — yalnızca TicariBelgeYonetimi.View gerektirir,
    //  TesisYonetimi/CariKartYonetimi/MuhasebeKdvIstisnaTanimlariYonetimi İSTEMEZ.
    // ──────────────────────────────────────────────

    [HttpGet("lookups/tesisler")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetTesisLookup(CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetTesislerAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookups/cari-kartlar")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetCariKartLookup(
        [FromQuery] int tesisId, [FromQuery] SatisBelgesiTipi belgeTipi, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetCariKartlarAsync(tesisId, belgeTipi, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookups/kdv-istisnalari")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetKdvIstisnaLookup(
        [FromQuery] TicariBelgeKdvIstisnaLookupFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetKdvIstisnalarAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpPost("lookups/iade-adaylari")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetIadeAdaylariLookup(
        [FromBody] TicariBelgeIadeAdayiFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetIadeAdaylariAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("lookups/kaynak-satirlar/{kaynakBelgeId:int}")]
    [Permission(StructurePermissions.TicariBelgeYonetimi.View)]
    public async Task<IActionResult> GetKaynakSatirLookup(
        int kaynakBelgeId, [FromQuery] int? mevcutBelgeId, CancellationToken cancellationToken)
    {
        var result = await _lookupService.GetKaynakSatirlarAsync(kaynakBelgeId, mevcutBelgeId, cancellationToken);
        return Ok(result);
    }
}
