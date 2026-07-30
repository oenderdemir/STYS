namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Satış belgesi satır ve muhasebe fişi tutarları için TEK, tutarlı hesaplama noktası.
/// Bu formülün SatisBelgesiService (satır oluşturma) ve
/// MuhasebeFisStratejileri altındaki stratejiler (fiş satırı üretimi) arasında
/// TEKRARLANMASINI önlemek için buraya çıkarılmıştır.
///
/// SatirToplami = Matrah + KdvTutari - TevkifatTutari + OtvTutari + OivTutari + KonaklamaVergisiTutari
///
/// NOT (vergi/matrah ilişkisi): Mevcut domain modelinde her vergi (KDV, ÖTV, ÖİV,
/// konaklama vergisi) satırın KENDİ Matrah alanı üzerinden BAĞIMSIZ olarak hesaplanır
/// (bkz. SatisBelgesiService.CreateSatirFromRequest) - ör. ÖTV'nin KDV matrahına dahil
/// edilip edilmediğine dair açık bir kural/alan YOKTUR. Bu hesaplayıcı bu ilişkiyi
/// DEĞİŞTİRMEZ; yalnızca zaten hesaplanmış tutarları doğru ve tek bir yerde TOPLAR.
/// </summary>
public static class SatisBelgesiTutarHesaplayici
{
    /// <summary>Projede kullanılan standart parasal yuvarlama: 2 ondalık, AwayFromZero.</summary>
    public static decimal Yuvarla(decimal deger) =>
        Math.Round(deger, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Bir satırın nihai (ödenecek) toplamını hesaplar. Sonuç 2 ondalık basamağa
    /// yuvarlanır; belge GenelToplam'ı bu (satır bazında zaten yuvarlanmış) değerlerin
    /// toplamından oluşmalıdır (bkz. SatisBelgesiService.HesaplaBelgeToplamlari) - böylece
    /// satır ve belge toplamları arasında ayrı bir üst-düzey yuvarlama nedeniyle kuruş
    /// farkı oluşmaz.
    /// </summary>
    public static decimal HesaplaSatirToplami(
        decimal matrah,
        decimal kdvTutari,
        decimal tevkifatTutari,
        decimal otvTutari,
        decimal oivTutari,
        decimal konaklamaVergisiTutari)
    {
        return Yuvarla(matrah + kdvTutari - tevkifatTutari + otvTutari + oivTutari + konaklamaVergisiTutari);
    }
}
