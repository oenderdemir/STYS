using Microsoft.AspNetCore.Mvc;
using STYS.OdaTemizlik.Dto;
using STYS.OdaTemizlik.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaTemizlik.Controllers;

public class OdaTemizlikController : UIController
{
    private readonly IOdaTemizlikService _odaTemizlikService;

    public OdaTemizlikController(IOdaTemizlikService odaTemizlikService)
    {
        _odaTemizlikService = odaTemizlikService;
    }

    [HttpGet("tesisler")]
    [Permission(StructurePermissions.OdaTemizlikYonetimi.View)]
    public async Task<ActionResult<List<OdaTemizlikTesisDto>>> GetTesisler(CancellationToken cancellationToken)
    {
        var tesisler = await _odaTemizlikService.GetErisilebilirTesislerAsync(cancellationToken);
        return Ok(tesisler);
    }

    [HttpGet("paged")]
    [Permission(StructurePermissions.OdaTemizlikYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdaTemizlikKayitDto>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int? tesisId,
        [FromQuery] string? durum,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var result = await _odaTemizlikService.GetPagedAsync(
            request,
            query,
            tesisId,
            durum,
            sortBy,
            sortDir,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{odaId:int}/baslat")]
    [Permission(StructurePermissions.OdaTemizlikYonetimi.Manage)]
    public async Task<ActionResult<OdaTemizlikKayitDto>> BaslatTemizlik(int odaId, CancellationToken cancellationToken)
    {
        var updated = await _odaTemizlikService.BaslatTemizlikAsync(odaId, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{odaId:int}/tamamla")]
    [Permission(StructurePermissions.OdaTemizlikYonetimi.Manage)]
    public async Task<ActionResult<OdaTemizlikKayitDto>> TamamlaTemizlik(int odaId, CancellationToken cancellationToken)
    {
        var updated = await _odaTemizlikService.TamamlaTemizlikAsync(odaId, cancellationToken);
        return Ok(updated);
    }
}
