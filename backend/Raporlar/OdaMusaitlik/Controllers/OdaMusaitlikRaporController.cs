using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.OdaMusaitlik.Dto;
using STYS.Raporlar.OdaMusaitlik.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.OdaMusaitlik.Controllers;

[Route("api/raporlar/oda-musaitlik")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OdaMusaitlikRaporController : UIController
{
    private readonly IOdaMusaitlikRaporService _odaMusaitlikRaporService;
    private readonly IOdaMusaitlikRaporExcelService _odaMusaitlikRaporExcelService;

    public OdaMusaitlikRaporController(
        IOdaMusaitlikRaporService odaMusaitlikRaporService,
        IOdaMusaitlikRaporExcelService odaMusaitlikRaporExcelService)
    {
        _odaMusaitlikRaporService = odaMusaitlikRaporService;
        _odaMusaitlikRaporExcelService = odaMusaitlikRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdaMusaitlikRaporuYonetimi.View)]
    public async Task<ActionResult<OdaMusaitlikRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] string? durum,
        [FromQuery] int? odaTipiId,
        [FromQuery] int? kapasite,
        CancellationToken cancellationToken)
    {
        var rapor = await _odaMusaitlikRaporService.GetRaporAsync(tesisId, baslangic, bitis, durum, odaTipiId, kapasite, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.OdaMusaitlikRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] string? durum,
        [FromQuery] int? odaTipiId,
        [FromQuery] int? kapasite,
        CancellationToken cancellationToken)
    {
        var bytes = await _odaMusaitlikRaporExcelService.OlusturAsync(tesisId, baslangic, bitis, durum, odaTipiId, kapasite, cancellationToken);
        var fileName = $"oda-musaitlik-raporu-{tesisId}-{baslangic:yyyyMMdd}-{bitis:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
