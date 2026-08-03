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
    /// Satır matrahını hesaplar: brüt tutardan (Miktar × BirimFiyat) indirim düşülür ve
    /// KDV hesaplanmadan ÖNCE 2 ondalık basamağa yuvarlanır. KDV, ÖTV, ÖİV ve konaklama
    /// vergisi bu YUVARLANMIŞ matrah üzerinden hesaplanır - brüt (yuvarlanmamış) tutar
    /// üzerinden değil (bkz. SatisBelgesiService.CreateSatirFromRequest).
    /// </summary>
    public static decimal HesaplaMatrah(decimal brutMatrah, decimal indirimTutari) =>
        Yuvarla(brutMatrah - indirimTutari);

    /// <summary>
    /// Satır KDV tutarını, zaten yuvarlanmış matrah üzerinden hesaplar ve sonucu 2 ondalık
    /// basamağa yuvarlar. Belge/gruplanmış toplamlar bu satır bazında yuvarlanmış değerlerin
    /// düz toplamı olmalıdır; toplu matrah üzerinden yeniden hesaplanmamalıdır (aksi halde
    /// satır bazlı yuvarlamadan doğan kuruş farkları belge toplamıyla tutarsızlaşabilir).
    /// </summary>
    public static decimal HesaplaKdvTutari(decimal matrah, decimal kdvOrani) =>
        Yuvarla(matrah * kdvOrani / 100m);

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

    /// <summary>
    /// Tek bir satırın belge toplamlarına katkısını taşıyan, karşılaştırma için yeterli
    /// alt kümeyi temsil eder. Renderer ve kesim öncesi kapı da (ileride) bu tipi kullanarak
    /// aynı doğrulama mantığını çağırabilir - toplam tutarlılık kuralı tek yerde tanımlıdır.
    /// </summary>
    public readonly record struct SatirTutarKatkisi(decimal Matrah, decimal KdvTutari, decimal SatirToplami);

    /// <summary>
    /// Belge düzeyi toplamların (ToplamMatrah/ToplamKdv/GenelToplam), aktif satırların
    /// (zaten satır bazında yuvarlanmış) değerlerinin düz toplamıyla tutarlı olup olmadığını
    /// doğrular. Karşılaştırma saklanmış (canonical) değerler üzerinden yapılır; hiçbir değer
    /// bu metot tarafından değiştirilmez veya yeniden yuvarlanmaz - yalnızca uyuşmazlıklar
    /// raporlanır. Boş liste dönerse belge toplamları tutarlıdır.
    /// </summary>
    public static IReadOnlyList<BelgeToplamUyusmazligi> DogrulaBelgeToplamlari(
        IEnumerable<SatirTutarKatkisi> aktifSatirlar,
        decimal toplamMatrah,
        decimal toplamKdv,
        decimal genelToplam)
    {
        var satirListesi = aktifSatirlar as IReadOnlyCollection<SatirTutarKatkisi> ?? aktifSatirlar.ToList();

        var hesaplananMatrah = satirListesi.Sum(s => s.Matrah);
        var hesaplananKdv = satirListesi.Sum(s => s.KdvTutari);
        var hesaplananGenelToplam = satirListesi.Sum(s => s.SatirToplami);

        var uyusmazliklar = new List<BelgeToplamUyusmazligi>();

        if (toplamMatrah != hesaplananMatrah)
        {
            uyusmazliklar.Add(new BelgeToplamUyusmazligi("ToplamMatrah", hesaplananMatrah, toplamMatrah));
        }

        if (toplamKdv != hesaplananKdv)
        {
            uyusmazliklar.Add(new BelgeToplamUyusmazligi("ToplamKdv", hesaplananKdv, toplamKdv));
        }

        if (genelToplam != hesaplananGenelToplam)
        {
            uyusmazliklar.Add(new BelgeToplamUyusmazligi("GenelToplam", hesaplananGenelToplam, genelToplam));
        }

        return uyusmazliklar;
    }

    /// <summary>Bir belge toplamı uyuşmazlığını taşır: hangi alan, beklenen (hesaplanan) ve mevcut (saklanmış) değer.</summary>
    public readonly record struct BelgeToplamUyusmazligi(string Alan, decimal HesaplananDeger, decimal MevcutDeger);
}
