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
    [Permission(StructurePermissions.EkHizmetYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetTesisDto>>> GetTesisler(CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetErisilebilirTesislerAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("tesis/{tesisId:int}")]
    [Permission(StructurePermissions.EkHizmetYonetimi.View)]
    public async Task<ActionResult<List<EkHizmetTarifeDto>>> GetByTesisId(int tesisId, CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.GetByTesisIdAsync(tesisId, cancellationToken);
        return Ok(items);
    }

    [HttpPut("tesis/{tesisId:int}")]
    [Permission(StructurePermissions.EkHizmetYonetimi.Manage)]
    public async Task<ActionResult<List<EkHizmetTarifeDto>>> UpsertByTesis(
        int tesisId,
        [FromBody] List<EkHizmetTarifeDto> tarifeler,
        CancellationToken cancellationToken)
    {
        var items = await _ekHizmetTarifeService.UpsertByTesisAsync(tesisId, tarifeler, cancellationToken);
        return Ok(items);
    }
}
