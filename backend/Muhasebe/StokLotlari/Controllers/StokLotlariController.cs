using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.StokLotlari.Dtos;
using STYS.Muhasebe.StokLotlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Muhasebe.StokLotlari.Controllers;

[Route("ui/muhasebe/stok-lotlari")]
public class StokLotlariController : UIController
{
    private readonly IStokLotSktUyariService _service;

    public StokLotlariController(IStokLotSktUyariService service)
    {
        _service = service;
    }

    [HttpGet("skt-uyarilari")]
    [Permission(StructurePermissions.StokHareketYonetimi.View)]
    public async Task<ActionResult<List<StokLotSktUyariDto>>> GetSktUyarilari([FromQuery] int tesisId, [FromQuery] int? depoId, [FromQuery] int? tasinirKartId, [FromQuery] bool? sadeceRiskli, CancellationToken cancellationToken)
        => Ok(await _service.GetSktUyarilariAsync(tesisId, depoId, tasinirKartId, sadeceRiskli ?? false, cancellationToken));
}
