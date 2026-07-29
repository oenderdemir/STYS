using Microsoft.AspNetCore.Mvc;
using STYS.Entegrasyonlar.Pavo.Dtos;
using STYS.Entegrasyonlar.Pavo.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pavo.Controllers;

[Route("ui/pavo")]
public sealed class PavoController : UIController
{
    private readonly IPavoService _service;

    public PavoController(IPavoService service)
    {
        _service = service;
    }

    [HttpGet("terminaller")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<PavoTerminalDto>>> GetTerminaller(
        [FromQuery] int? tesisId,
        [FromQuery] int? kasaBankaHesapId,
        CancellationToken cancellationToken) =>
        Ok(await _service.GetTerminallerAsync(tesisId, kasaBankaHesapId, cancellationToken));

    [HttpPost("terminaller")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PavoTerminalDto>> CreateTerminal(
        [FromBody] PavoTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetTerminalAsync(null, request, cancellationToken));

    [HttpPut("terminaller/{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PavoTerminalDto>> UpdateTerminal(
        int id,
        [FromBody] PavoTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetTerminalAsync(id, request, cancellationToken));

    [HttpPost("terminaller/{id:int}/eslestir")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PavoTerminalDto>> EslesmeBaslat(int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeBaslatAsync(id, cancellationToken));

    [HttpPost("terminaller/{id:int}/eslestirme-kontrol")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PavoTerminalDto>> EslesmeKontrol(int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeKontrolAsync(id, cancellationToken));

    [HttpPost("odemeler")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PavoOdemeIslemiDto>> OdemeBaslat(
        [FromBody] PavoOdemeBaslatRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.OdemeBaslatAsync(request, cancellationToken));

    [HttpGet("odemeler/{id:int}")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PavoOdemeIslemiDto>> OdemeDurumu(int id, CancellationToken cancellationToken) =>
        Ok(await _service.OdemeDurumuAsync(id, cancellationToken));

    [HttpGet("odemeler/bekleyen")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<PavoOdemeIslemiDto?>> BekleyenOdeme(
        [FromQuery] int rezervasyonId,
        CancellationToken cancellationToken) =>
        Ok(await _service.BekleyenOdemeAsync(rezervasyonId, cancellationToken));
}
