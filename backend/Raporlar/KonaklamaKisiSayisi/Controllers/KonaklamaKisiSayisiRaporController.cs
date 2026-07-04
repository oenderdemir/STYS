using Microsoft.AspNetCore.Mvc;
using STYS.Licensing;
using STYS.Raporlar.KonaklamaKisiSayisi.Dto;
using STYS.Raporlar.KonaklamaKisiSayisi.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Licensing.AspNetCore;

namespace STYS.Raporlar.KonaklamaKisiSayisi.Controllers;

[Route("api/raporlar/konaklama-kisi-sayisi")]
[ApiController]
[RequiresLicensedModule(StysLicensedModules.Rezervasyon)]
public class KonaklamaKisiSayisiRaporController : UIController
{
    private readonly IKonaklamaKisiSayisiRaporService _konaklamaKisiSayisiRaporService;
    private readonly IKonaklamaKisiSayisiRaporExcelService _konaklamaKisiSayisiRaporExcelService;

    public KonaklamaKisiSayisiRaporController(
        IKonaklamaKisiSayisiRaporService konaklamaKisiSayisiRaporService,
        IKonaklamaKisiSayisiRaporExcelService konaklamaKisiSayisiRaporExcelService)
    {
        _konaklamaKisiSayisiRaporService = konaklamaKisiSayisiRaporService;
        _konaklamaKisiSayisiRaporExcelService = konaklamaKisiSayisiRaporExcelService;
    }

    [HttpGet]
    [Permission(StructurePermissions.KonaklamaKisiSayisiRaporuYonetimi.View)]
    public async Task<ActionResult<KonaklamaKisiSayisiRaporDto>> GetRapor(
        [FromQuery] int tesisId,
        [FromQuery] int ay,
        [FromQuery] int baslangicYil,
        [FromQuery] int bitisYil,
        CancellationToken cancellationToken)
    {
        var rapor = await _konaklamaKisiSayisiRaporService.GetRaporAsync(tesisId, ay, baslangicYil, bitisYil, cancellationToken);
        return Ok(rapor);
    }

    [HttpGet("excel")]
    [Permission(StructurePermissions.KonaklamaKisiSayisiRaporuYonetimi.View)]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] int tesisId,
        [FromQuery] int ay,
        [FromQuery] int baslangicYil,
        [FromQuery] int bitisYil,
        CancellationToken cancellationToken)
    {
        var bytes = await _konaklamaKisiSayisiRaporExcelService.OlusturAsync(tesisId, ay, baslangicYil, bitisYil, cancellationToken);
        var fileName = $"konaklama-kisi-sayisi-{tesisId}-{baslangicYil}-{bitisYil}-{ay:00}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
