using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.OdaTipiDoluluk.Dto;
using STYS.Raporlar.OdaTipiDoluluk.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.OdaTipiDoluluk.Controllers;

[Route("api/raporlar/oda-tipi-doluluk")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OdaTipiDolulukRaporController : UIController
{
    private readonly IOdaTipiDolulukRaporService _odaTipiDolulukRaporService;
    private readonly IOdaTipiDolulukRaporExcelService _odaTipiDolulukRaporExcelService;

    public OdaTipiDolulukRaporController(
        IOdaTipiDolulukRaporService odaTipiDolulukRaporService,
        IOdaTipiDolulukRaporExcelService odaTipiDolulukRaporExcelService)
    {
        _odaTipiDolulukRaporService = odaTipiDolulukRaporService;
        _odaTipiDolulukRaporExcelService = odaTipiDolulukRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaTipiDolulukRaporuYonetimi.View)]
    public async Task<ActionResult<OdaTipiDolulukRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        CancellationToken cancellationToken)
    {
        var rapor = await _odaTipiDolulukRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.OdaTipiDolulukRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        CancellationToken cancellationToken)
    {
        var bytes = await _odaTipiDolulukRaporExcelService.OlusturAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);
        var fileName = $"oda-tipi-doluluk-raporu-{tesisId}-{baslangic:yyyyMMdd}-{bitis:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
