using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.OdemeIzleme.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.OdemeIzleme.Controllers;

/// <summary>
/// Ödeme İzleme/Araştırma - SALT OKUNUR arama/mutabakat destek ekranı. Hiçbir endpoint ödeme/fiş/
/// valör kaydı oluşturmaz veya değiştirmez; tümü StructurePermissions.OdemeIzlemeYonetimi.View
/// yetkisi ister. Otomatik taşıma/düzeltme/mahsup YOKTUR.
/// </summary>
[Route("ui/muhasebe/odeme-izleme")]
public class OdemeIzlemeController : UIController
{
    private readonly IOdemeIzlemeService _service;
    private readonly IOdemeCaprazAramaService _caprazAramaService;

    public OdemeIzlemeController(IOdemeIzlemeService service, IOdemeCaprazAramaService caprazAramaService)
    {
        _service = service;
        _caprazAramaService = caprazAramaService;
    }

    /// <summary>Capraz-kaynak arastirma: odeme belgesi, cari hareket, POS valor, kasa/banka hareketi
    /// ve muhasebe fisi kaynaklarindan BAGIMSIZ aday uretir ve iliski kopukluklarini raporlar.</summary>
    [HttpGet("capraz-arama")]
    [Permission(StructurePermissions.OdemeIzlemeYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdemeAdayiDto>>> CaprazAra(
        [FromQuery] PagedRequest request, [FromQuery] OdemeCaprazAramaFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _caprazAramaService.AraAsync(request, filter, cancellationToken));

    [HttpGet]
    [Permission(StructurePermissions.OdemeIzlemeYonetimi.View)]
    public async Task<ActionResult<PagedResult<OdemeAramaSatiriDto>>> Ara(
        [FromQuery] PagedRequest request, [FromQuery] OdemeAramaFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.AraAsync(request, filter, cancellationToken));

    [HttpGet("{id:int}")]
    [Permission(StructurePermissions.OdemeIzlemeYonetimi.View)]
    public async Task<ActionResult<OdemeDetayDto>> GetDetay(int id, CancellationToken cancellationToken)
        => Ok(await _service.GetDetayAsync(id, cancellationToken));

    [HttpGet("cari-hareket-dokumu")]
    [Permission(StructurePermissions.OdemeIzlemeYonetimi.View)]
    public async Task<ActionResult<CariHareketDokumDto>> GetCariHareketDokumu([FromQuery] CariHareketDokumFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetCariHareketDokumuAsync(filter, cancellationToken));

    [HttpGet("karsilastir")]
    [Permission(StructurePermissions.OdemeIzlemeYonetimi.View)]
    public async Task<ActionResult<List<BeyanEdilenOdemeEslesmeDto>>> Karsilastir([FromQuery] BeyanEdilenOdemeKarsilastirmaFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.KarsilastirAsync(filter, cancellationToken));
}
