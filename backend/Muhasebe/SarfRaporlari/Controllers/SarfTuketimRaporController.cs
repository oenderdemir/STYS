using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.SarfRaporlari.Dtos;
using STYS.Muhasebe.SarfRaporlari.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.SarfRaporlari.Controllers;

[Route("ui/muhasebe/sarf-raporlari")]
public class SarfTuketimRaporController : UIController
{
    private readonly ISarfTuketimRaporService _raporService;
    private readonly ISarfTuketimRaporExcelService _excelService;

    public SarfTuketimRaporController(
        ISarfTuketimRaporService raporService,
        ISarfTuketimRaporExcelService excelService)
    {
        _raporService = raporService;
        _excelService = excelService;
    }

    [HttpGet("detay")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<PagedResult<SarfTuketimDetayRaporSatirDto>>> GetDetay(
        [FromQuery] PagedRequest request,
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _raporService.GetDetayAsync(request, filter, cancellationToken));

    [HttpGet("malzeme-ozet")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<List<SarfTuketimMalzemeOzetDto>>> GetMalzemeOzet(
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _raporService.GetMalzemeOzetAsync(filter, cancellationToken));

    [HttpGet("kullanim-yeri-ozet")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<ActionResult<List<SarfTuketimKullanimYeriOzetDto>>> GetKullanimYeriOzet(
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
        => Ok(await _raporService.GetKullanimYeriOzetAsync(filter, cancellationToken));

    [HttpGet("detay/excel")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<IActionResult> ExportDetayExcel(
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var bytes = await _excelService.ExportDetayAsync(filter, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sarf-tuketim-detay-{filter.TesisId}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpGet("malzeme-ozet/excel")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<IActionResult> ExportMalzemeOzetExcel(
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var bytes = await _excelService.ExportMalzemeOzetAsync(filter, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sarf-tuketim-malzeme-ozet-{filter.TesisId}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    [HttpGet("kullanim-yeri-ozet/excel")]
    [Permission(StructurePermissions.SarfYonetimi.View)]
    public async Task<IActionResult> ExportKullanimYeriOzetExcel(
        [FromQuery] SarfTuketimRaporFilterDto filter,
        CancellationToken cancellationToken)
    {
        var bytes = await _excelService.ExportKullanimYeriOzetAsync(filter, cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"sarf-tuketim-kullanim-yeri-ozet-{filter.TesisId}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }
}
