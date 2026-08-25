using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

public interface IStokMaliyetKatmaniRestoreService
{
    /// <summary>
    /// Orijinal bir çıkış hareketinin tükettiği FIFO/LIFO maliyet katmanlarını, iptal ters kaydı
    /// (Giris) üzerinde YENİ incoming layer olarak geri yükler. Orijinal tüketim kayıtları ve
    /// katman geçmişi değiştirilmez; maliyet orijinal tüketimin BirimMaliyet değerinden taşınır.
    /// Çıkış hareketine ait katman tüketimi yoksa no-op'tur.
    /// </summary>
    Task RestoreLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto reversalMovement, CancellationToken cancellationToken = default);

    /// <summary>
    /// KISMİ iade/geri-alma için: FIFO/LIFO katman tüketimi varsa iade edilen miktar için YENİ bir
    /// incoming layer oluşturur (maliyet orijinal çıkış hareketinin MaliyetBirimFiyat snapshot'ından
    /// taşınır). Orijinal tüketim kayıtları DEĞİŞTİRİLMEZ. Katman tüketimi yoksa (weighted-average)
    /// no-op'tur — maliyet snapshot iade hareketinin üzerinde taşınır.
    /// </summary>
    Task RestorePartialLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto iadeMovement, decimal iadeMiktari, CancellationToken cancellationToken = default);
}
