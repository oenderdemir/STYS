using Microsoft.AspNetCore.Mvc;
using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.EkHizmetler.Controllers;

public class EkHizmetTarifeController : UIController
{
    private readonly IEkHizmetTarifeService _ekHizmetTarifeService;

    public EkHizmetTarifeController(IEkHizmetTarifeService ekHizmetTarifeService)
    {
        _ekHizmetTarifeService = ekHizmetTarifeService;
    }

    [HttpGet("tesisler")]
    [Permission(
        StructurePermissions.EkHizmetTesisAtamaYonetimi.View,
        StructurePermissions.EkHizmetTarifeYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetTesisDto>>> GetTesisler(CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetErisilebilirTesislerAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("global-tanimlar")]
    [Permission(StructurePermissions.EkHizmetTanimYonetimi.View)]
    public async Task<ActionResult<List<GlobalEkHizmetTanimiDto>>> GetGlobalTanimlar(CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetGlobalTanimlarAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("global-tanimlar/{id:int}")]
    [Permission(StructurePermissions.EkHizmetTanimYonetimi.View)]
    public async Task<ActionResult<GlobalEkHizmetTanimiDto>> GetGlobalTanimById(int id, CancellationToken cancellationToken)
    {
        var item = await _ekHizmetTarifeService.GetGlobalTanimByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpPost("global-tanimlar")]
    [Permission(StructurePermissions.EkHizmetTanimYonetimi.Manage)]
    public async Task<ActionResult<GlobalEkHizmetTanimiDto>> CreateGlobalTanim([FromBody] GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken)
    {
        var item = await _ekHizmetTarifeService.AddGlobalTanimAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetGlobalTanimById), new { id = item.Id }, item);
    }

    [HttpPut("global-tanimlar/{id:int}")]
    [Permission(StructurePermissions.EkHizmetTanimYonetimi.Manage)]
    public async Task<ActionResult<GlobalEkHizmetTanimiDto>> UpdateGlobalTanim(int id, [FromBody] GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken)
    {
        var item = await _ekHizmetTarifeService.UpdateGlobalTanimAsync(id, dto, cancellationToken);
        return Ok(item);
    }

    [HttpDelete("global-tanimlar/{id:int}")]
    [Permission(StructurePermissions.EkHizmetTanimYonetimi.Manage)]
    public async Task<IActionResult> DeleteGlobalTanim(int id, CancellationToken cancellationToken)
    {
        await _ekHizmetTarifeService.DeleteGlobalTanimAsync(id, cancellationToken);
        return Ok();
    }

    [HttpGet("tesis/{tesisId:int}/atamalar")]
    [Permission(StructurePermissions.EkHizmetTesisAtamaYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetTesisAtamaDto>>> GetTesisAtamalari(int tesisId, CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetTesisAtamalariAsync(tesisId, cancellationToken);
        return Ok(items);
    }

    [HttpPut("tesis/{tesisId:int}/atamalar")]
    [Permission(StructurePermissions.EkHizmetTesisAtamaYonetimi.Manage)]
    public async Task<ActionResult<List<EkHizmetTesisAtamaDto>>> KaydetTesisAtamalari(
        int tesisId,
        [FromBody] EkHizmetTesisAtamaKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.KaydetTesisAtamalariAsync(tesisId, request.GlobalEkHizmetTanimiIds, cancellationToken);
        return Ok(items);
    }

    [HttpGet("tesis/{tesisId:int}/hizmetler")]
    [Permission(StructurePermissions.EkHizmetTarifeYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetDto>>> GetHizmetlerByTesisId(int tesisId, CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetHizmetlerByTesisIdAsync(tesisId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("tesis/{tesisId:int}/tarifeler")]
    [Permission(StructurePermissions.EkHizmetTarifeYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetTarifeDto>>> GetTarifelerByTesisId(int tesisId, CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetByTesisIdAsync(tesisId, cancellationToken);
        return Ok(items);
    }

    [HttpPut("tesis/{tesisId:int}/tarifeler")]
    [Permission(StructurePermissions.EkHizmetTarifeYonetimi.Manage)]
    public async Task<ActionResult<List<EkHizmetTarifeDto>>> UpsertTarifelerByTesis(int tesisId, [FromBody] List<EkHizmetTarifeDto> tarifeler, CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.UpsertByTesisAsync(tesisId, tarifeler, cancellationToken);
        return Ok(items);
    }
}
