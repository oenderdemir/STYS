using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.OrtalamaKonaklamaSuresi.Dto;
using STYS.Raporlar.OrtalamaKonaklamaSuresi.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.OrtalamaKonaklamaSuresi.Controllers;

[Route("api/raporlar/ortalama-konaklama-suresi")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class OrtalamaKonaklamaSuresiRaporController : UIController
{
    private readonly IOrtalamaKonaklamaSuresiRaporService _ortalamaKonaklamaSuresiRaporService;
    private readonly IOrtalamaKonaklamaSuresiRaporExcelService _ortalamaKonaklamaSuresiRaporExcelService;

    public OrtalamaKonaklamaSuresiRaporController(
        IOrtalamaKonaklamaSuresiRaporService ortalamaKonaklamaSuresiRaporService,
        IOrtalamaKonaklamaSuresiRaporExcelService ortalamaKonaklamaSuresiRaporExcelService)
    {
        _ortalamaKonaklamaSuresiRaporService = ortalamaKonaklamaSuresiRaporService;
        _ortalamaKonaklamaSuresiRaporExcelService = ortalamaKonaklamaSuresiRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.OrtalamaKonaklamaSuresiRaporuYonetimi.View)]
    public async Task<ActionResult<OrtalamaKonaklamaSuresiRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        CancellationToken cancellationToken)
    {
        var rapor = await _ortalamaKonaklamaSuresiRaporService.GetRaporAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.OrtalamaKonaklamaSuresiRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime baslangic,
        [FromQuery] DateTime bitis,
        [FromQuery] int? odaTipiId,
        CancellationToken cancellationToken)
    {
        var bytes = await _ortalamaKonaklamaSuresiRaporExcelService.OlusturAsync(tesisId, baslangic, bitis, odaTipiId, cancellationToken);
        var fileName = $"ortalama-konaklama-suresi-raporu-{tesisId}-{baslangic:yyyyMMdd}-{bitis:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
