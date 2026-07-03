using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.Dto;
using STYS.Raporlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.Controllers;

[Route("api/raporlar")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OdaDolulukRaporController : UIController
{
    private readonly IOdaDolulukRaporService _odaDolulukRaporService;
    private readonly IOdaDolulukRaporExcelService _odaDolulukRaporExcelService;

    public OdaDolulukRaporController(
        IOdaDolulukRaporService odaDolulukRaporService,
        IOdaDolulukRaporExcelService odaDolulukRaporExcelService)
    {
        _odaDolulukRaporService = odaDolulukRaporService;
        _odaDolulukRaporExcelService = odaDolulukRaporExcelService;
    }

    [HttpGet("oda-doluluk-aylik")]
    [Permission(StructurePermissions.OdaDolulukRaporuYonetimi.View)]
    public async Task<ActionResult<AylikOdaDolulukRaporDto>> GetAylikOdaDolulukRaporu(
        [FromQuery] int tesisId,
        [FromQuery] int yil,
        [FromQuery] int ay,
        [FromQuery] bool maskele,
        CancellationToken cancellationToken)
    {
        var rapor = await _odaDolulukRaporService.GetAylikOdaDolulukRaporuAsync(tesisId, yil, ay, maskele, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("oda-doluluk-aylik/excel")]
    [Permission(StructurePermissions.OdaDolulukRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] int yil,
        [FromQuery] int ay,
        [FromQuery] bool maskele,
        [FromQuery] string? matrisYonu,
        CancellationToken cancellationToken)
    {
        var bytes = await _odaDolulukRaporExcelService.OlusturAsync(tesisId, yil, ay, maskele, matrisYonu, cancellationToken);
        var fileName = $"oda-doluluk-raporu-{tesisId}-{yil}-{ay:00}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // TODO: GET api/raporlar/oda-doluluk-aylik/pdf - PDF export endpointi eklenecek.
}
