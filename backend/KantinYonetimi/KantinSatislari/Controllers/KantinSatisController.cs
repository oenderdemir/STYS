using Microsoft.AspNetCore.Mvc;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.KantinYonetimi.KantinSatislari.Controllers;

[Route("ui/kantin-satis")]
public class KantinSatisController : UIController
{
    private readonly IKantinSatisService _service;
    private readonly IKantinSatisMuhasebeFisService _muhasebeFisService;

    public KantinSatisController(IKantinSatisService service, IKantinSatisMuhasebeFisService muhasebeFisService)
    {
        _service = service;
        _muhasebeFisService = muhasebeFisService;
    }

    [HttpGet]
    [Permission(StructurePermissions.KantinSatisYonetimi.View)]
    public async Task<ActionResult<List<KantinSatisDto>>> GetList([FromQuery] int? tesisId, [FromQuery] int? kantinId, CancellationToken cancellationToken)
        => Ok(await _service.GetListAsync(tesisId, kantinId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.View)]
    public async Task<ActionResult<KantinSatisDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> Create([FromBody] CreateKantinSatisRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddAsync(new KantinSatisDto
        {
            KantinId = request.KantinId,
            SatisNoktasiId = request.SatisNoktasiId,
            SatisTarihi = request.SatisTarihi ?? DateTime.UtcNow,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> Update(int id, [FromBody] UpdateKantinSatisRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(new KantinSatisDto
        {
            Id = id,
            SatisTarihi = request.SatisTarihi ?? default,
            Aciklama = request.Aciklama
        }, cancellationToken));

    [HttpPost("{id:int}/satirlar")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> AddSatir(int id, [FromBody] AddKantinSatisSatirRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddSatirAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/satirlar/{satirId:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> UpdateSatir(int id, int satirId, [FromBody] UpdateKantinSatisSatirRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateSatirAsync(id, satirId, request, cancellationToken));

    [HttpDelete("{id:int}/satirlar/{satirId:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<IActionResult> DeleteSatir(int id, int satirId, CancellationToken cancellationToken)
    {
        await _service.DeleteSatirAsync(id, satirId, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/odemeler")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> AddOdeme(int id, [FromBody] AddKantinSatisOdemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.AddOdemeAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/odemeler/{odemeId:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> UpdateOdeme(int id, int odemeId, [FromBody] UpdateKantinSatisOdemeRequest request, CancellationToken cancellationToken)
        => Ok(await _service.UpdateOdemeAsync(id, odemeId, request, cancellationToken));

    [HttpDelete("{id:int}/odemeler/{odemeId:int}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<IActionResult> DeleteOdeme(int id, int odemeId, CancellationToken cancellationToken)
    {
        await _service.DeleteOdemeAsync(id, odemeId, cancellationToken);
        return Ok();
    }

    [HttpGet("kantin/{kantinId:int}/urun-barkod/{barkod}")]
    [Permission(StructurePermissions.KantinSatisYonetimi.View)]
    public async Task<ActionResult<KantinSatisBarkodUrunDto>> GetUrunByBarkod(int kantinId, string barkod, CancellationToken cancellationToken)
    {
        var item = await _service.GetAktifUrunByBarkodAsync(kantinId, barkod, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:int}/kesinlestir")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> Kesinlestir(int id, CancellationToken cancellationToken)
        => Ok(await _service.KesinlestirAsync(id, cancellationToken));

    [HttpPost("{id:int}/muhasebe-fisi-olustur")]
    [Permission(StructurePermissions.KantinSatisYonetimi.Create)]
    public async Task<ActionResult<KantinSatisDto>> MuhasebeFisiOlustur(int id, CancellationToken cancellationToken)
        => Ok(await _muhasebeFisService.MuhasebeFisiOlusturAsync(id, cancellationToken));
}
