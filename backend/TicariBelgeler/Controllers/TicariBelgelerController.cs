using Microsoft.AspNetCore.Mvc;
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

    public TicariBelgelerController(ITicariBelgeService service)
    {
        _service = service;
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
}
