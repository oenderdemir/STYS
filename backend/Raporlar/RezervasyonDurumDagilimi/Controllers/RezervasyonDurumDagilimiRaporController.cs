using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.RezervasyonDurumDagilimi.Dto;
using STYS.Raporlar.RezervasyonDurumDagilimi.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.RezervasyonDurumDagilimi.Controllers;

[Route("api/raporlar/rezervasyon-durum-dagilimi")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class RezervasyonDurumDagilimiRaporController : UIController
{
    private readonly IRezervasyonDurumDagilimiRaporService _rezervasyonDurumDagilimiRaporService;
    private readonly IRezervasyonDurumDagilimiRaporExcelService _rezervasyonDurumDagilimiRaporExcelService;

    public RezervasyonDurumDagilimiRaporController(
        IRezervasyonDurumDagilimiRaporService rezervasyonDurumDagilimiRaporService,
        IRezervasyonDurumDagilimiRaporExcelService rezervasyonDurumDagilimiRaporExcelService)
    {
        _rezervasyonDurumDagilimiRaporService = rezervasyonDurumDagilimiRaporService;
        _rezervasyonDurumDagilimiRaporExcelService = rezervasyonDurumDagilimiRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.RezervasyonDurumDagilimiRaporuYonetimi.View)]
    public async Task<ActionResult<RezervasyonDurumDagilimiRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        [FromQuery] string? durum,
        CancellationToken cancellationToken)
    {
        var rapor = await _rezervasyonDurumDagilimiRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, durum, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.RezervasyonDurumDagilimiRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        [FromQuery] string? durum,
        CancellationToken cancellationToken)
    {
        var bytes = await _rezervasyonDurumDagilimiRaporExcelService.OlusturAsync(tesisId, baslangic, bitis, odaTipiId, durum, cancellationToken);
        var fileName = $"rezervasyon-durum-dagilimi-raporu-{tesisId}-{baslangic:yyyyMMdd}-{bitis:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
