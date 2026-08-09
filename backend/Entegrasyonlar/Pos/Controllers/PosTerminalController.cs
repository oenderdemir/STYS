using Microsoft.AspNetCore.Mvc;
using STYS.Entegrasyonlar.Pos.Dtos;
using STYS.Entegrasyonlar.Pos.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Entegrasyonlar.Pos.Controllers;

[Route("ui/pos/cihazlar/{cihazId:int}/terminaller")]
public sealed class PosTerminalController : UIController
{
    private readonly PosTerminalService _service;

    public PosTerminalController(PosTerminalService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<List<PosTerminalDto>>> GetAll(int cihazId, CancellationToken cancellationToken) =>
        Ok(await _service.GetByCihazAsync(cihazId, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.View)]
    public async Task<ActionResult<PosTerminalDto>> GetById(int cihazId, int id, CancellationToken cancellationToken) =>
        Ok(await _service.GetByIdAsync(cihazId, id, cancellationToken));

    [HttpPost]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> Create(
        int cihazId,
        [FromBody] PosTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetAsync(cihazId, null, request, cancellationToken));

    [HttpPut("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> Update(
        int cihazId,
        int id,
        [FromBody] PosTerminalKaydetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _service.KaydetAsync(cihazId, id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult> Delete(int cihazId, int id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(cihazId, id, cancellationToken);
        return Ok();
    }

    [HttpPost("{id:int}/eslestir")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> EslesmeBaslat(int cihazId, int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeBaslatAsync(cihazId, id, cancellationToken));

    [HttpPost("{id:int}/eslestirme-kontrol")]
    [Permission(StructurePermissions.KasaBankaHesapYonetimi.Manage)]
    public async Task<ActionResult<PosTerminalDto>> EslesmeKontrol(int cihazId, int id, CancellationToken cancellationToken) =>
        Ok(await _service.EslesmeKontrolAsync(cihazId, id, cancellationToken));
}
