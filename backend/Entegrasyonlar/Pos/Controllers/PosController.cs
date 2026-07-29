using Microsoft.AspNetCore.Mvc;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pos.Controllers;

[Route("ui/pos")]
public sealed class PosController : UIController
{
    private readonly IPosService _service;

    public PosController(IPosService service)
    {
        _service = service;
    }

    [HttpGet("saglayicilar")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public ActionResult<List<PosSaglayiciDto>> GetSaglayicilar() => Ok(_service.GetSaglayicilar());

    [HttpGet("terminaller")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<PosTerminalDto>>> GetTerminaller(
        [FromQuery] int? tesisId,
        [FromQuery] int? kasaBankaHesapId,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetTerminallerAsync(tesisId, kasaBankaHesapId, cancellationToken));

    [HttpPost("terminaller")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> CreateTerminal(
        [FromBody] PosTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetTerminalAsync(null, request, cancellationToken));

    [HttpPut("terminaller/{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> UpdateTerminal(
        int id,
        [FromBody] PosTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetTerminalAsync(id, request, cancellationToken));

    [HttpPost("terminaller/{id:int}/eslestir")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> EslesmeBaslat(int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeBaslatAsync(id, cancellationToken));

    [HttpPost("terminaller/{id:int}/eslestirme-kontrol")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> EslesmeKontrol(int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeKontrolAsync(id, cancellationToken));

    [HttpPost("odemeler")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto>> OdemeBaslat(
        [FromBody] PosOdemeBaslatRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.OdemeBaslatAsync(request, cancellationToken));

    [HttpGet("odemeler/{id:int}")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto>> OdemeDurumu(int id, CancellationToken cancellationToken) =>
        Ok(await _service.OdemeDurumuAsync(id, cancellationToken));

    [HttpGet("odemeler/bekleyen")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PosOdemeIslemiDto?>> BekleyenOdeme(
        [FromQuery] int rezervasyonId,
        CancellationToken cancellationToken) =>
        Ok(await _service.BekleyenOdemeAsync(rezervasyonId, cancellationToken));
}
