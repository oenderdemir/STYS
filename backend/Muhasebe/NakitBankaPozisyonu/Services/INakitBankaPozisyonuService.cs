using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Services;

/// <summary>
/// Nakit ve Banka Pozisyonu ekrani icin SALT-OKUNUR sorgu servisi. Hicbir yazma islemi yapmaz -
/// yeni muhasebe fisi olusturmaz, valor/bakiye kayitlarini degistirmez. Tum hesaplamalar burada
/// TEK KAYNAK olarak yapilir; Angular tarafinda tekrar uretilmez.
/// </summary>
public interface INakitBankaPozisyonuService
{
    /// <summary>Ozet + hesap listeleri + uyarilari TEK sorgu calistirmasindan uretir (bkz.
    /// NakitBankaPozisyonuDto). Ekranin ana veri kaynagidir.</summary>
    Task<NakitBankaPozisyonuDto> GetPozisyonAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen banka/IBAN hesabi icin, rapor tarihinden itibaren gun bazinda bekleyen
    /// POS valor GUNLUK OZETLERINI (detay satirlari OLMADAN) dondurur.</summary>
    Task<BankaValorTakvimiDto> GetValorTakvimiAsync(int kasaBankaHesapId, DateOnly? raporTarihi, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen banka/IBAN hesabi ve TEK bir valor tarihi icin sayfali detay
    /// kayitlarini dondurur. valorDurumu verilirse yalnizca bu durumdaki kayitlar listelenir (bu
    /// filtre yalnizca bu detay sorgusunu etkiler, ozet/takvim toplamlarini ETKILEMEZ).</summary>
    Task<PagedResult<ValorDetayDto>> GetValorGunDetaylariAsync(
        int kasaBankaHesapId, DateOnly valorTarihi, string? valorDurumu, int sayfa, int sayfaBoyutu, CancellationToken cancellationToken = default);
}
