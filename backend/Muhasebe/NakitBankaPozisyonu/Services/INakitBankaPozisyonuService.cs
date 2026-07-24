using STYS.Muhasebe.NakitBankaPozisyonu.Dtos;

namespace STYS.Muhasebe.NakitBankaPozisyonu.Services;

/// <summary>
/// Nakit ve Banka Pozisyonu ekrani icin SALT-OKUNUR sorgu servisi. Hicbir yazma islemi yapmaz -
/// yeni muhasebe fisi olusturmaz, valor/bakiye kayitlarini degistirmez. Tum hesaplamalar burada
/// TEK KAYNAK olarak yapilir; Angular tarafinda tekrar uretilmez.
/// </summary>
public interface INakitBankaPozisyonuService
{
    Task<NakitBankaPozisyonuOzetDto> GetOzetAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default);

    Task<NakitBankaPozisyonuHesaplarDto> GetHesaplarAsync(NakitBankaPozisyonuFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen banka/IBAN hesabi icin, rapor tarihinden itibaren gun bazinda bekleyen
    /// (Durum=ValorBekliyor) POS valor takvimini dondurur.</summary>
    Task<BankaValorTakvimiDto> GetValorTakvimiAsync(int kasaBankaHesapId, DateOnly? raporTarihi, CancellationToken cancellationToken = default);
}
