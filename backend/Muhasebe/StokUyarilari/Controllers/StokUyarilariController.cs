using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokUyarilari.Dtos;
using STYS.Muhasebe.StokUyarilari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.StokUyarilari.Controllers;

[Route("ui/muhasebe/stok-uyarilari")]
public class StokUyarilariController : UIController
{
    private readonly IStokUyariService _service;

    public StokUyarilariController(IStokUyariService service)
    {
        _service = service;
    }

    [HttpGet]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokUyariDto>>> GetList([FromQuery] int tesisId, [FromQuery] int? depoId, [FromQuery] int? tasinirKartId, [FromQuery] bool? sadeceRiskli, CancellationToken cancellationToken)
        => Ok(await _service.GetStokUyarilariAsync(tesisId, depoId, tasinirKartId, sadeceRiskli ?? false, cancellationToken));
}
