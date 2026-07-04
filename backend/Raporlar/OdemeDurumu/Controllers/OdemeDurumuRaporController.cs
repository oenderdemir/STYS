using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.OdemeDurumu.Dto;
using STYS.Raporlar.OdemeDurumu.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.OdemeDurumu.Controllers;

[Route("api/raporlar/odeme-durumu")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OdemeDurumuRaporController : UIController
{
    private readonly IOdemeDurumuRaporService _odemeDurumuRaporService;
    private readonly IOdemeDurumuRaporExcelService _odemeDurumuRaporExcelService;

    public OdemeDurumuRaporController(
        IOdemeDurumuRaporService odemeDurumuRaporService,
        IOdemeDurumuRaporExcelService odemeDurumuRaporExcelService)
    {
        _odemeDurumuRaporService = odemeDurumuRaporService;
        _odemeDurumuRaporExcelService = odemeDurumuRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OdemeDurumuRaporuYonetimi.View)]
    public async Task<ActionResult<OdemeDurumuRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] string? odemeDurumu,
        CancellationToken cancellationToken)
    {
        var rapor = await _odemeDurumuRaporService.GetRaporAsync(tesisId, baslangic, bitis, odemeDurumu, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.OdemeDurumuRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] string? odemeDurumu,
        CancellationToken cancellationToken)
    {
        var bytes = await _odemeDurumuRaporExcelService.OlusturAsync(tesisId, baslangic, bitis, odemeDurumu, cancellationToken);
        var fileName = $"odeme-durumu-raporu-{tesisId}-{baslangic:yyyyMMdd}-{bitis:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
