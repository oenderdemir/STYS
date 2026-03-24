using Microsoft.AspNetCore.Mvc;
using STYS.Rezervasyonlar.Dto;
using STYS.Rezervasyonlar.Reporting;
using STYS.Rezervasyonlar.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;

namespace STYS.Rezervasyonlar.Controllers;

public class RezervasyonController : UIController
{
    private readonly IRezervasyonService _rezervasyonService;

    public RezervasyonController(IRezervasyonService rezervasyonService)
    {
        _rezervasyonService = rezervasyonService;
    }

    [HttpGet("tesisler")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonTesisDto>>> GetTesisler(CancellationToken cancellationToken)
    {
        var tesisler = await _rezervasyonService.GetErisilebilirTesislerAsync(cancellationToken);
        return Ok(tesisler);
    }

    [HttpGet("oda-tipleri")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonOdaTipiDto>>> GetOdaTipleriByTesis([FromQuery] int tesisId, CancellationToken cancellationToken)
    {
        var odaTipleri = await _rezervasyonService.GetOdaTipleriByTesisAsync(tesisId, cancellationToken);
        return Ok(odaTipleri);
    }

    [HttpGet("misafir-tipleri")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonMisafirTipiDto>>> GetMisafirTipleri(CancellationToken cancellationToken)
    {
        var misafirTipleri = await _rezervasyonService.GetMisafirTipleriAsync(cancellationToken);
        return Ok(misafirTipleri);
    }

    [HttpGet("konaklama-tipleri")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonKonaklamaTipiDto>>> GetKonaklamaTipleri(CancellationToken cancellationToken)
    {
        var konaklamaTipleri = await _rezervasyonService.GetKonaklamaTipleriAsync(cancellationToken);
        return Ok(konaklamaTipleri);
    }

    [HttpGet("indirim-kurallari")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonIndirimKuraliSecenekDto>>> GetIndirimKurallari(
        [FromQuery] int tesisId,
        [FromQuery] int misafirTipiId,
        [FromQuery] int konaklamaTipiId,
        [FromQuery] DateTime baslangicTarihi,
        [FromQuery] DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var indirimKurallari = await _rezervasyonService.GetUygulanabilirIndirimKurallariAsync(
            tesisId,
            misafirTipiId,
            konaklamaTipiId,
            baslangicTarihi,
            bitisTarihi,
            cancellationToken);
        return Ok(indirimKurallari);
    }

    [HttpGet("kayitlar")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonListeDto>>> GetKayitlar([FromQuery] int? tesisId, CancellationToken cancellationToken)
    {
        var kayitlar = await _rezervasyonService.GetRezervasyonlarAsync(tesisId, cancellationToken);
        return Ok(kayitlar);
    }

    [HttpGet("dashboard")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<RezervasyonDashboardDto>> GetDashboard(
        [FromQuery] int tesisId,
        [FromQuery] DateTime? tarih,
        [FromQuery] DateTime? kpiBaslangicTarihi,
        [FromQuery] DateTime? kpiBitisTarihi,
        CancellationToken cancellationToken)
    {
        var dashboard = await _rezervasyonService.GetGunlukDashboardAsync(
            tesisId,
            tarih,
            kpiBaslangicTarihi,
            kpiBitisTarihi,
            cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/detay")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<RezervasyonDetayDto>> GetDetay([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var detay = await _rezervasyonService.GetRezervasyonDetayAsync(rezervasyonId, cancellationToken);
        if (detay is null)
        {
            return NotFound();
        }

        return Ok(detay);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/degisiklik-gecmisi")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<RezervasyonDegisiklikGecmisiDto>>> GetDegisiklikGecmisi([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GetDegisiklikGecmisiAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/konaklayan-plani")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<RezervasyonKonaklayanPlanDto>> GetKonaklayanPlani([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var plan = await _rezervasyonService.GetKonaklayanPlaniAsync(rezervasyonId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        return Ok(plan);
    }

    [HttpPut("kayitlar/{rezervasyonId:int}/konaklayan-plani")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKonaklayanPlanDto>> KaydetKonaklayanPlani(
        [FromRoute] int rezervasyonId,
        [FromBody] RezervasyonKonaklayanPlanKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var plan = await _rezervasyonService.KaydetKonaklayanPlaniAsync(rezervasyonId, request, cancellationToken);
        return Ok(plan);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/oda-degisim-secenekleri")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdaDegisimSecenekDto>> GetOdaDegisimSecenekleri([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GetOdaDegisimSecenekleriAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpPut("kayitlar/{rezervasyonId:int}/oda-degisim")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKayitSonucDto>> KaydetOdaDegisimi(
        [FromRoute] int rezervasyonId,
        [FromBody] RezervasyonOdaDegisimKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.KaydetOdaDegisimiAsync(rezervasyonId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("uygun-odalar")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<UygunOdaDto>>> GetUygunOdalar([FromBody] UygunOdaAramaRequestDto request, CancellationToken cancellationToken)
    {
        var uygunOdalar = await _rezervasyonService.GetUygunOdalarAsync(request, cancellationToken);
        return Ok(uygunOdalar);
    }

    [HttpPost("senaryo-ara")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<List<KonaklamaSenaryoDto>>> GetKonaklamaSenaryolari([FromBody] KonaklamaSenaryoAramaRequestDto request, CancellationToken cancellationToken)
    {
        var senaryolar = await _rezervasyonService.GetKonaklamaSenaryolariAsync(request, cancellationToken);
        return Ok(senaryolar);
    }

    [HttpPost("senaryo-fiyat-hesapla")]
    [Permission(StructurePermissions.RezervasyonYonetimi.View)]
    public async Task<ActionResult<SenaryoFiyatHesaplamaSonucuDto>> HesaplaSenaryoFiyati([FromBody] SenaryoFiyatHesaplaRequestDto request, CancellationToken cancellationToken)
    {
        var sonuc = await _rezervasyonService.HesaplaSenaryoFiyatiAsync(request, cancellationToken);
        return Ok(sonuc);
    }

    [HttpPost]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKayitSonucDto>> Create([FromBody] RezervasyonKaydetRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.KaydetAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kayitlar/{rezervasyonId:int}/check-in")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKayitSonucDto>> TamamlaCheckIn([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.TamamlaCheckInAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/check-in-kontrol")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonCheckInKontrolDto>> GetCheckInKontrol([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GetCheckInKontrolAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kayitlar/{rezervasyonId:int}/check-out")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKayitSonucDto>> TamamlaCheckOut([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.TamamlaCheckOutAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kayitlar/{rezervasyonId:int}/iptal")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonKayitSonucDto>> IptalEt([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.IptalEtAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/odeme-ozeti")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdemeOzetDto>> GetOdemeOzeti([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GetOdemeOzetiAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("kayitlar/{rezervasyonId:int}/ek-hizmet-secenekleri")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonEkHizmetSecenekleriDto>> GetEkHizmetSecenekleri([FromRoute] int rezervasyonId, CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GetEkHizmetSecenekleriAsync(rezervasyonId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kayitlar/{rezervasyonId:int}/ek-hizmetler")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdemeOzetDto>> KaydetEkHizmet(
        [FromRoute] int rezervasyonId,
        [FromBody] RezervasyonEkHizmetKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.KaydetEkHizmetAsync(rezervasyonId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPut("kayitlar/{rezervasyonId:int}/ek-hizmetler/{ekHizmetId:int}")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdemeOzetDto>> GuncelleEkHizmet(
        [FromRoute] int rezervasyonId,
        [FromRoute] int ekHizmetId,
        [FromBody] RezervasyonEkHizmetKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.GuncelleEkHizmetAsync(rezervasyonId, ekHizmetId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("kayitlar/{rezervasyonId:int}/ek-hizmetler/{ekHizmetId:int}")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdemeOzetDto>> SilEkHizmet(
        [FromRoute] int rezervasyonId,
        [FromRoute] int ekHizmetId,
        CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.SilEkHizmetAsync(rezervasyonId, ekHizmetId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("kayitlar/{rezervasyonId:int}/odemeler")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<ActionResult<RezervasyonOdemeOzetDto>> KaydetOdeme(
        [FromRoute] int rezervasyonId,
        [FromBody] RezervasyonOdemeKaydetRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _rezervasyonService.KaydetOdemeAsync(rezervasyonId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("odeme-raporu/excel")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<IActionResult> ExportOdemeRaporuExcel(
        [FromQuery] int[] tesisIds,
        [FromQuery] DateTime baslangicTarihi,
        [FromQuery] DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var report = await _rezervasyonService.GetOdemeRaporuAsync(tesisIds, baslangicTarihi, bitisTarihi, cancellationToken);
        var fileBytes = OdemeRaporExportBuilder.BuildExcel(report);
        var fileName = $"odeme-raporu-{baslangicTarihi:yyyyMMdd}-{bitisTarihi:yyyyMMdd}.xls";
        return File(fileBytes, "application/vnd.ms-excel", fileName);
    }

    [HttpGet("odeme-raporu/pdf")]
    [Permission(StructurePermissions.RezervasyonYonetimi.Manage)]
    public async Task<IActionResult> ExportOdemeRaporuPdf(
        [FromQuery] int[] tesisIds,
        [FromQuery] DateTime baslangicTarihi,
        [FromQuery] DateTime bitisTarihi,
        CancellationToken cancellationToken)
    {
        var report = await _rezervasyonService.GetOdemeRaporuAsync(tesisIds, baslangicTarihi, bitisTarihi, cancellationToken);
        var fileBytes = OdemeRaporExportBuilder.BuildPdf(report);
        var fileName = $"odeme-raporu-{baslangicTarihi:yyyyMMdd}-{bitisTarihi:yyyyMMdd}.pdf";
        return File(fileBytes, "application/pdf", fileName);
    }
}
