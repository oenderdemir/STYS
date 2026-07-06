using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.GecikenCheckIn.Dto;
using STYS.Raporlar.GecikenCheckIn.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.GecikenCheckIn.Controllers;

[Route("api/raporlar/geciken-check-in")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class GecikenCheckInRaporController : UIController
{
    private readonly IGecikenCheckInRaporService _gecikenCheckInRaporService;
    private readonly IGecikenCheckInRaporExcelService _gecikenCheckInRaporExcelService;

    public GecikenCheckInRaporController(
        IGecikenCheckInRaporService gecikenCheckInRaporService,
        IGecikenCheckInRaporExcelService gecikenCheckInRaporExcelService)
    {
        _gecikenCheckInRaporService = gecikenCheckInRaporService;
        _gecikenCheckInRaporExcelService = gecikenCheckInRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.GecikenCheckInRaporuYonetimi.View)]
    public async Task<ActionResult<GecikenCheckInRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] DateTime? referansTarihi,
        [FromQuery] int? odaTipiId,
        [FromQuery] string? gecikmeDurumu,
        CancellationToken cancellationToken)
    {
        var rapor = await _gecikenCheckInRaporService.GetRaporAsync(tesisId, referansTarihi, odaTipiId, gecikmeDurumu, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.GecikenCheckInRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] DateTime? referansTarihi,
        [FromQuery] int? odaTipiId,
        [FromQuery] string? gecikmeDurumu,
        CancellationToken cancellationToken)
    {
        var bytes = await _gecikenCheckInRaporExcelService.OlusturAsync(tesisId, referansTarihi, odaTipiId, gecikmeDurumu, cancellationToken);
        var referansTarihiIcinDosyaAdi = referansTarihi ?? DateTime.Now;
        var fileName = $"geciken-check-in-raporu-{tesisId}-{referansTarihiIcinDosyaAdi:yyyyMMdd}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
