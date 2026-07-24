using Microsoft.AspNetCore.Mvc;
using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using STYS.Muhasebe.NakitBankaPozisyonu.Services;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Controllers;

/// <summary>
/// Nakit ve Banka Pozisyonu - SALT OKUNUR izleme/raporlama ekrani. Hicbir endpoint veri
/// degistirmez; tumu StructurePermissions.NakitBankaPozisyonuYonetimi.View yetkisi ister.
/// </summary>
[Route("ui/muhasebe/nakit-banka-pozisyonu")]
public class NakitBankaPozisyonuController : UIController
{
    private readonly INakitBankaPozisyonuService _service;

    public NakitBankaPozisyonuController(INakitBankaPozisyonuService service)
    {
        _service = service;
    }

    /// <summary>Ozet + kasa/banka hesap listeleri + uyarilari TEK cagride dondurur (eskiden ayri
    /// olan /ozet ve /hesaplar endpoint'leri, ayni pahali sorgunun iki kez calismasina yol
    /// aciyordu - bu endpoint TEK bir GetPozisyonAsync cagrisiyla ikisini de karsilar).</summary>
    [HttpGet]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<NakitBankaPozisyonuDto>> GetPozisyon([FromQuery] NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken)
        => Ok(await _service.GetPozisyonAsync(filter, cancellationToken));

    [HttpGet("banka-hesaplari/{id:int}/valor-takvimi")]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<BankaValorTakvimiDto>> GetValorTakvimi(int id, [FromQuery] DateOnly? raporTarihi, CancellationToken cancellationToken)
        => Ok(await _service.GetValorTakvimiAsync(id, raporTarihi, cancellationToken));

    /// <summary>Tek bir gunun sayfali detay kayitlari - kullanici takvimde bir gunu actiginda
    /// ayrica cagrilir, tum kayitlar tek seferde yuklenmez.</summary>
    [HttpGet("banka-hesaplari/{id:int}/valor-takvimi/{valorTarihi}/detaylar")]
    [Permission(StructurePermissions.NakitBankaPozisyonuYonetimi.View)]
    public async Task<ActionResult<PagedResult<ValorDetayDto>>> GetValorGunDetaylari(
        int id, DateOnly valorTarihi, [FromQuery] string? valorDurumu, [FromQuery] int sayfa, [FromQuery] int sayfaBoyutu, CancellationToken cancellationToken)
        => Ok(await _service.GetValorGunDetaylariAsync(id, valorTarihi, valorDurumu, sayfa, sayfaBoyutu, cancellationToken));
}
