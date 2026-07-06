using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.GunlukGirisCikis.Dto;
using STYS.Raporlar.GunlukGirisCikis.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.GunlukGirisCikis.Controllers;

[Route("api/raporlar/gunluk-giris-cikis")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class GunlukGirisCikisRaporController : UIController
{
    private readonly IGunlukGirisCikisRaporService _gunlukGirisCikisRaporService;
    private readonly IGunlukGirisCikisRaporExcelService _gunlukGirisCikisRaporExcelService;

    public GunlukGirisCikisRaporController(
        IGunlukGirisCikisRaporService gunlukGirisCikisRaporService,
        IGunlukGirisCikisRaporExcelService gunlukGirisCikisRaporExcelService)
    {
        _gunlukGirisCikisRaporService = gunlukGirisCikisRaporService;
        _gunlukGirisCikisRaporExcelService = gunlukGirisCikisRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.GunlukGirisCikisRaporuYonetimi.View)]
    public async Task<ActionResult<GunlukGirisCikisRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime tarih,
        [FromQuery] string? listeTipi,
        CancellationToken cancellationToken)
    {
        var rapor = await _gunlukGirisCikisRaporService.GetRaporAsync(tesisId, tarih, listeTipi, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.GunlukGirisCikisRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime tarih,
        [FromQuery] string? listeTipi,
        CancellationToken cancellationToken)
    {
        var bytes = await _gunlukGirisCikisRaporExcelService.OlusturAsync(tesisId, tarih, listeTipi, cancellationToken);
        var fileName = $"gunluk-giris-cikis-listesi-{tesisId}-{tarih:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
