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
}
