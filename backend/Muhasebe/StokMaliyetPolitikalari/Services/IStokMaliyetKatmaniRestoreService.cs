using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Services;

/// <summary>Kısmi iade/geri-alma planının tek bir layer segmenti.</summary>
public sealed record StokMaliyetRestoreSegment(decimal Miktar, decimal BirimMaliyet)
{
    public decimal Tutar => Math.Round(Miktar * BirimMaliyet, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Orijinal çıkış hareketinin tüketim kayıtlarından (StokMaliyetKatmanTuketimleri) deterministik
/// olarak üretilen kısmi iade planı. Segmentlerin toplam maliyeti ile oluşturulacak StokHareket'in
/// MaliyetTutari birebir eşleşir.
/// </summary>
public sealed record StokMaliyetRestorePlan(
    string MaliyetYontemi,
    IReadOnlyList<StokMaliyetRestoreSegment> Segmentler,
    decimal ToplamMaliyet,
    decimal EfektifBirimMaliyet);

public interface IStokMaliyetKatmaniRestoreService
{
    /// <summary>
    /// Orijinal bir çıkış hareketinin tükettiği FIFO/LIFO maliyet katmanlarını, iptal ters kaydı
    /// (Giris) üzerinde YENİ incoming layer olarak geri yükler. Orijinal tüketim kayıtları ve
    /// katman geçmişi değiştirilmez; maliyet orijinal tüketimin BirimMaliyet değerinden taşınır.
    /// Çıkış hareketine ait katman tüketimi yoksa no-op'tur. (K3C1 tam iptal.)
    /// </summary>
    Task RestoreLayeredCostIfNeededAsync(StokHareket originalMovement, StokHareketDto reversalMovement, CancellationToken cancellationToken = default);

    /// <summary>
    /// Orijinal çıkış hareketinin tüketim kayıtlarından, halihazırda geri yüklenmiş miktar
    /// (alreadyRestoredQuantity) ATLANARAK returnQuantity kadar deterministik kısmi iade planı
    /// üretir. FIFO ve LIFO için orijinal tüketim sırası korunur; katman tüketimi yoksa
    /// (weighted-average) null döner.
    /// </summary>
    Task<StokMaliyetRestorePlan?> PlanPartialRestoreAsync(
        int originalMovementId,
        decimal alreadyRestoredQuantity,
        decimal returnQuantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen planın segmentlerini iade hareketi üzerinde YENİ incoming layer olarak yazar.
    /// Orijinal tüketim kayıtları değiştirilmez.
    /// </summary>
    Task RestorePlannedLayersAsync(StokMaliyetRestorePlan plan, StokHareketDto iadeMovement, CancellationToken cancellationToken = default);
}
