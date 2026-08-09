using Microsoft.AspNetCore.Mvc;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pos.Controllers;

[Route("ui/pos")]
public sealed class PosCihaziController : UIController
{
    private readonly PosCihaziService _service;

    public PosCihaziController(PosCihaziService service) { _service = service; }

    [HttpGet("cihazlar")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<PosCihaziDto>>> GetAll([FromQuery] int? tesisId, CancellationToken ct) =>
        Ok(await _service.GetAllAsync(tesisId, ct));

    [HttpGet("cihazlar/{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<PosCihaziDto>> GetById(int id, CancellationToken ct) =>
        Ok(await _service.GetByIdAsync(id, ct));

    [HttpPost("cihazlar")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Create([FromBody] PosCihaziKaydetRequest req, CancellationToken ct) =>
        Ok(await _service.CreateAsync(req, ct));

    [HttpPut("cihazlar/{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosCihaziDto>> Update(int id, [FromBody] PosCihaziKaydetRequest req, CancellationToken ct) =>
        Ok(await _service.UpdateAsync(id, req, ct));

    [HttpDelete("cihazlar/{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct) { await _service.DeleteAsync(id, ct); return Ok(); }
}
