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
    private readonly IOdaDolulukRaporPdfService _odaDolulukRaporPdfService;

    public OdaDolulukRaporController(
        IOdaDolulukRaporService odaDolulukRaporService,
        IOdaDolulukRaporExcelService odaDolulukRaporExcelService,
        IOdaDolulukRaporPdfService odaDolulukRaporPdfService)
    {
        _odaDolulukRaporService = odaDolulukRaporService;
        _odaDolulukRaporExcelService = odaDolulukRaporExcelService;
        _odaDolulukRaporPdfService = odaDolulukRaporPdfService;
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

    [HttpGet("oda-doluluk-aylik/pdf")]
    [Permission(StructurePermissions.OdaDolulukRaporuYonetimi.View)]
    public async Task<IActionResult> ExportPdf(
        [FromQuery] int tesisId,
        [FromQuery] int yil,
        [FromQuery] int ay,
        [FromQuery] bool maskele,
        CancellationToken cancellationToken)
    {
        var bytes = await _odaDolulukRaporPdfService.OlusturAsync(tesisId, yil, ay, maskele, cancellationToken);
        var fileName = $"oda-doluluk-raporu-{tesisId}-{yil}-{ay:00}.pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
