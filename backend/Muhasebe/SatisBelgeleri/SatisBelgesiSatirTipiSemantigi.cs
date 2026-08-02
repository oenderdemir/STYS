using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// SatisBelgesiSatirTipi'nin taşınır kart/depo alanlarıyla ilişkisini doğrulayan TEK, saf ve
/// merkezi kural kaynağı - bkz. görev "satır tipi semantiğini muhasebe transaction'ında da
/// doğrula". Hem satır YAZMA anında (SatisBelgesiService.ValidateSatirRequestAsync, create/update)
/// hem de muhasebe fişi oluşturma anında, transaction içinde YENİDEN OKUNAN gerçek DB satırları
/// üzerinde (SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync) kullanılır - bu kural iki
/// yerde AYRI AYRI yeniden üretilmez. İkinci kullanım noktası özellikle önemlidir: servis
/// katmanındaki yazma-anı doğrulaması atlatılmış (ör. eski veri, migration, doğrudan SQL ile
/// eklenmiş) satırların sessizce yanlış hesaba (ör. bir "hizmet" satırının stok hesabına)
/// aktarılmasını engeller - fail-closed, hiçbir fiş/cari/stok kaydı oluşturulmadan ÖNCE.
/// </summary>
public static class SatisBelgesiSatirTipiSemantigi
{
    /// <summary>
    /// Tek bir satırın tipini ve taşınır kart/depo alanlarını doğrular; kural ihlalinde
    /// <see cref="BaseException"/> (400) fırlatır - hiçbir durumda sessizce bir varsayılana
    /// düşülmez (fail-closed).
    /// </summary>
    public static void Dogrula(SatisBelgesiSatirTipi satirTipi, int? tasinirKartId, int? depoId, int siraNo)
    {
        // Tanımsız (Enum.IsDefined ile doğrulanmayan) bir satır tipi - ör. veritabanına doğrudan
        // yazılmış, hiçbir SatisBelgesiSatirTipi üyesine karşılık gelmeyen bir tamsayı - açıkça
        // reddedilir; "bilinmeyen tip = hizmet" gibi bir varsayıma ASLA düşülmez.
        if (!Enum.IsDefined(satirTipi))
            throw new BaseException($"Geçersiz satır tipi: {(int)satirTipi}. (SıraNo: {siraNo})", errorCode: 400);

        if (satirTipi == SatisBelgesiSatirTipi.Urun)
        {
            if (!tasinirKartId.HasValue)
                throw new BaseException($"Ürün satırlarında taşınır kart seçimi zorunludur. (SıraNo: {siraNo})", errorCode: 400);

            if (!depoId.HasValue)
                throw new BaseException($"Ürün satırlarında depo seçimi zorunludur. (SıraNo: {siraNo})", errorCode: 400);
        }
        else
        {
            if (tasinirKartId.HasValue)
                throw new BaseException($"Ürün dışındaki satırlarda taşınır kart seçilemez. (SıraNo: {siraNo})", errorCode: 400);

            if (depoId.HasValue)
                throw new BaseException($"Ürün dışındaki satırlarda depo seçilemez. (SıraNo: {siraNo})", errorCode: 400);
        }
    }
}
