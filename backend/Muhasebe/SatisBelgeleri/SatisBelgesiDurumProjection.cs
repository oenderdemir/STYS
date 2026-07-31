using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Tek, saf ve test edilebilir yardımcı: (BelgeTipi + mevcut/otoriter SatisBelgesiDurumu)
/// çiftinden üç AYRIŞTIRILMIŞ, henüz otoriter OLMAYAN durum alanını (TicariDurum, MuhasebeDurumu,
/// FaturalamaDurumu) türetir. Bu sınıf hiçbir DB/servis bağımlılığı içermez; SatisBelgesiService
/// bu projeksiyonu her üretim Durum atamasından SONRA çağırıp sonucu birlikte yazar (dual-write) -
/// dağınık, birbirinden farklılaşabilecek kopya eşleme mantığı OLUŞTURULMAZ.
///
/// ÖNEMLİ: Bu turda (expand/compatibility projection aşaması) SatisBelgesiDurumu HÂLÂ otoriter
/// karar kaynağıdır - bu sınıfın ürettiği üç alan yalnızca BİRLİKTE YAZILIR, hiçbir üretim karar
/// kontrolü bu alanları OKUMAZ. Otoriter geçiş sonraki bir aşamadadır.
/// </summary>
public static class SatisBelgesiDurumProjection
{
    /// <summary>
    /// Ticari (hazırlık) durumu: Taslak ve IptalEdildi kendi karşılıklarına eşlenir; MUHASEBE REDDİ
    /// dahil diğer TÜM mevcut durumlar Hazir'e eşlenir - muhasebe reddi ticari belgenin kendisinin
    /// reddi SAYILMAZ (bkz. TicariBelgeDurumu doc'u); bu yüzden burada bir "Reddedildi" değeri YOKTUR.
    /// </summary>
    public static TicariBelgeDurumu ProjeTicariDurum(SatisBelgesiDurumu durum) => durum switch
    {
        SatisBelgesiDurumu.Taslak => TicariBelgeDurumu.Taslak,
        SatisBelgesiDurumu.IptalEdildi => TicariBelgeDurumu.IptalEdildi,
        _ => TicariBelgeDurumu.Hazir
    };

    /// <summary>
    /// Muhasebeleştirme durumu: SatisBelgesiDurumu'nun TÜM (7) değeri açıkça eşlenir - bilinmeyen/
    /// gelecekte eklenecek bir değer SESSİZCE bir varsayılana düşürülmez, fail-closed olarak
    /// ArgumentOutOfRangeException fırlatılır (bkz. ResolveKdvIslemYonu ile aynı yaklaşım).
    /// </summary>
    public static TicariBelgeMuhasebeDurumu ProjeMuhasebeDurumu(SatisBelgesiDurumu durum) => durum switch
    {
        SatisBelgesiDurumu.Taslak => TicariBelgeMuhasebeDurumu.Bekliyor,
        SatisBelgesiDurumu.MuhasebeOnayinda => TicariBelgeMuhasebeDurumu.Onayda,
        SatisBelgesiDurumu.MuhasebeOnaylandi => TicariBelgeMuhasebeDurumu.Onaylandi,
        SatisBelgesiDurumu.Reddedildi => TicariBelgeMuhasebeDurumu.Reddedildi,
        SatisBelgesiDurumu.FaturaKesildi => TicariBelgeMuhasebeDurumu.Onaylandi,
        SatisBelgesiDurumu.MusteriyeGonderildi => TicariBelgeMuhasebeDurumu.Onaylandi,
        SatisBelgesiDurumu.IptalEdildi => TicariBelgeMuhasebeDurumu.IptalEdildi,
        _ => throw new ArgumentOutOfRangeException(nameof(durum), durum, "Bilinmeyen SatisBelgesiDurumu değeri.")
    };

    /// <summary>
    /// Faturalama/gönderim durumu: ÖNCELİKLE mevcut durum geçmişi (FaturaKesildi/
    /// MusteriyeGonderildi/IptalEdildi) belge tipinden BAĞIMSIZ olarak korunur - bu üç durum,
    /// halihazırda gerçekleşmiş bir olayı temsil eder ve BelgeTipi'nin yönüne bakılmaksızın aynen
    /// yansıtılır. Bunların dışında: yalnızca STYS tarafından düzenlenen giden belgeler
    /// (SatisFaturasi, AlisIadeFaturasi - bkz. SatisBelgesiTipiExtensions.StysTarafindanDuzenlenirMi)
    /// için faturalama süreci "uygulanabilir" sayılır; diğer TÜM tipler (AlisFaturasi,
    /// SatisIadeFaturasi, FaturaTaslagi, Proforma, legacy IadeFaturasi) her zaman Uygulanamaz'dır -
    /// legacy IadeFaturasi'nin yönü TAHMİN EDİLMEZ/dönüştürülmez.
    /// </summary>
    public static TicariBelgeFaturalamaDurumu ProjeFaturalamaDurumu(SatisBelgesiTipi belgeTipi, SatisBelgesiDurumu durum)
    {
        switch (durum)
        {
            case SatisBelgesiDurumu.FaturaKesildi:
                return TicariBelgeFaturalamaDurumu.Kesildi;

            case SatisBelgesiDurumu.MusteriyeGonderildi:
                return TicariBelgeFaturalamaDurumu.MusteriyeGonderildi;

            case SatisBelgesiDurumu.IptalEdildi:
                return TicariBelgeFaturalamaDurumu.IptalEdildi;
        }

        if (!belgeTipi.StysTarafindanDuzenlenirMi())
            return TicariBelgeFaturalamaDurumu.Uygulanamaz;

        return durum == SatisBelgesiDurumu.MuhasebeOnaylandi
            ? TicariBelgeFaturalamaDurumu.KesimBekliyor
            : TicariBelgeFaturalamaDurumu.Baslatilmadi;
    }

    /// <summary>Üç projeksiyonu tek çağrıda üretir - SatisBelgesiService'teki dual-write noktalarının kullandığı tek giriş noktasıdır.</summary>
    public static (TicariBelgeDurumu TicariDurum, TicariBelgeMuhasebeDurumu MuhasebeDurumu, TicariBelgeFaturalamaDurumu FaturalamaDurumu) Proje(
        SatisBelgesiTipi belgeTipi, SatisBelgesiDurumu durum)
        => (ProjeTicariDurum(durum), ProjeMuhasebeDurumu(durum), ProjeFaturalamaDurumu(belgeTipi, durum));

    /// <summary>Zaten elde bir SatisBelgesi entity'si varsa, üç projeksiyon alanını DOĞRUDAN üzerine yazar.</summary>
    public static void UygulaVeYaz(SatisBelgesi belge)
    {
        var (ticari, muhasebe, faturalama) = Proje(belge.BelgeTipi, belge.Durum);
        belge.TicariDurum = ticari;
        belge.MuhasebeDurumu = muhasebe;
        belge.FaturalamaDurumu = faturalama;
    }
}
