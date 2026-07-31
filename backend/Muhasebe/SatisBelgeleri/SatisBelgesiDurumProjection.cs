using STYS.Muhasebe.SatisBelgeleri.Entities;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Tek, saf ve test edilebilir yardımcı — durum ayrıştırmasının HER İKİ yönünü barındırır:
///
/// 1) OTORİTER yön (ÜRETİM tarafından kullanılır): TicariDurum + MuhasebeDurumu + FaturalamaDurumu
///    ARTIK otoriter iş durumudur. <see cref="OtoriterDurumlariAta"/> bu üç alanı atar, kombinasyonu
///    doğrular (tanımsız/çelişkili bir kombinasyon SESSİZCE bir varsayılana düşürülmez, fail-closed
///    <see cref="InvalidOperationException"/> fırlatılır) ve eski <see cref="SatisBelgesi.Durum"/>
///    alanını BU üç alandan (compatibility projection ile) türetir. SatisBelgesiService'teki TÜM
///    üretim durum geçişleri BU metodu kullanır - üç alan ve eski Durum'un ayrı ayrı, dağınık
///    biçimde atanmasına İZİN VERİLMEZ.
///
/// 2) LEGACY/uyumluluk yönü (yalnızca migration, eski veri ve geriye dönük projection testleri
///    içindir - ÜRETİM KARAR MEKANİZMASI DEĞİLDİR): <see cref="ProjeTicariDurum"/>,
///    <see cref="ProjeMuhasebeDurumu"/>, <see cref="ProjeFaturalamaDurumu"/>, <see cref="Proje"/>,
///    <see cref="UygulaVeYaz"/> ve <see cref="DurumuAtaVeProjekteEt"/> eski (BelgeTipi + Durum)
///    çiftinden üç alanı türetir - bu yön yalnızca MakeSeparatedSatisBelgesiStatusesAuthoritative
///    migration'ının backfill'i ve eski→yeni projection birim testleri için KORUNUR.
/// </summary>
public static class SatisBelgesiDurumProjection
{
    // ═════════════════════════════════════════════════════════════
    // OTORİTER YÖN — üretim tarafından kullanılır
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// ATOMİK ve TEK üretim yardımcısı: üç otoriter alanı atar, kombinasyonu doğrular (bkz.
    /// <see cref="ProjeLegacyDurum"/> - geçersiz kombinasyonda hiçbir alan YARIM/tutarsız
    /// bırakılmadan, hiçbir atama yapılmadan ÖNCE exception fırlatılır) ve eski
    /// <see cref="SatisBelgesi.Durum"/> alanını bu üç alandan türetir.
    /// </summary>
    public static void OtoriterDurumlariAta(
        SatisBelgesi belge,
        TicariBelgeDurumu ticariDurum,
        TicariBelgeMuhasebeDurumu muhasebeDurumu,
        TicariBelgeFaturalamaDurumu faturalamaDurumu)
    {
        // Legacy Durum ÖNCE hesaplanır (ve geçersiz kombinasyonda BURADA fırlatılır) - böylece
        // dört alandan hiçbiri, geçersiz bir kombinasyon durumunda KISMEN atanmış olarak kalmaz.
        var legacyDurum = ProjeLegacyDurum(ticariDurum, muhasebeDurumu, faturalamaDurumu);

        belge.TicariDurum = ticariDurum;
        belge.MuhasebeDurumu = muhasebeDurumu;
        belge.FaturalamaDurumu = faturalamaDurumu;
        belge.Durum = legacyDurum;
    }

    /// <summary>
    /// Üç OTORİTER alandan eski (geriye uyumluluk) <see cref="SatisBelgesiDurumu"/> değerini
    /// türetir - "yeni → eski" compatibility projection. Yalnızca görev tanımında listelenen 7
    /// GEÇERLİ kombinasyon kabul edilir; tanımsız veya çelişkili herhangi bir kombinasyon (ör.
    /// TicariDurum=Taslak + MuhasebeDurumu=Onayda gibi asla birlikte bulunmaması gereken bir
    /// eşleşme) SESSİZCE bir varsayılana DÜŞÜRÜLMEZ - fail-closed olarak
    /// <see cref="InvalidOperationException"/> fırlatılır.
    /// </summary>
    public static SatisBelgesiDurumu ProjeLegacyDurum(
        TicariBelgeDurumu ticariDurum,
        TicariBelgeMuhasebeDurumu muhasebeDurumu,
        TicariBelgeFaturalamaDurumu faturalamaDurumu)
    {
        return (ticariDurum, muhasebeDurumu, faturalamaDurumu) switch
        {
            (TicariBelgeDurumu.IptalEdildi, TicariBelgeMuhasebeDurumu.IptalEdildi, TicariBelgeFaturalamaDurumu.IptalEdildi)
                => SatisBelgesiDurumu.IptalEdildi,

            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.MusteriyeGonderildi)
                => SatisBelgesiDurumu.MusteriyeGonderildi,

            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Kesildi)
                => SatisBelgesiDurumu.FaturaKesildi,

            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Reddedildi, TicariBelgeFaturalamaDurumu.Baslatilmadi)
                => SatisBelgesiDurumu.Reddedildi,
            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Reddedildi, TicariBelgeFaturalamaDurumu.Uygulanamaz)
                => SatisBelgesiDurumu.Reddedildi,

            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.KesimBekliyor)
                => SatisBelgesiDurumu.MuhasebeOnaylandi,
            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onaylandi, TicariBelgeFaturalamaDurumu.Uygulanamaz)
                => SatisBelgesiDurumu.MuhasebeOnaylandi,

            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, TicariBelgeFaturalamaDurumu.Baslatilmadi)
                => SatisBelgesiDurumu.MuhasebeOnayinda,
            (TicariBelgeDurumu.Hazir, TicariBelgeMuhasebeDurumu.Onayda, TicariBelgeFaturalamaDurumu.Uygulanamaz)
                => SatisBelgesiDurumu.MuhasebeOnayinda,

            (TicariBelgeDurumu.Taslak, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Baslatilmadi)
                => SatisBelgesiDurumu.Taslak,
            (TicariBelgeDurumu.Taslak, TicariBelgeMuhasebeDurumu.Bekliyor, TicariBelgeFaturalamaDurumu.Uygulanamaz)
                => SatisBelgesiDurumu.Taslak,

            _ => throw new InvalidOperationException(
                "Tanımsız veya çelişkili durum kombinasyonu: " +
                $"TicariDurum={ticariDurum}, MuhasebeDurumu={muhasebeDurumu}, FaturalamaDurumu={faturalamaDurumu}.")
        };
    }

    /// <summary>
    /// FaturalamaDurumu'nun "henüz fatura kesim süreci başlamamış/uygulanamaz" değerini belge
    /// tipine göre üretir - yalnızca STYS tarafından düzenlenen giden belgeler (SatisFaturasi,
    /// AlisIadeFaturasi) için Baslatilmadi, diğer TÜM tipler için Uygulanamaz döner. Taslak,
    /// MuhasebeOnayinda ve Reddedildi (legacy karşılıkları) durumlarının ÜÇÜ de bu AYNI formülü
    /// kullanır - henüz onaylanmamış/reddedilmiş bir belgenin faturalama süreci "başlamış"
    /// SAYILMAZ.
    /// </summary>
    public static TicariBelgeFaturalamaDurumu ProjeBaslangicFaturalamaDurumu(SatisBelgesiTipi belgeTipi)
        => belgeTipi.StysTarafindanDuzenlenirMi()
            ? TicariBelgeFaturalamaDurumu.Baslatilmadi
            : TicariBelgeFaturalamaDurumu.Uygulanamaz;

    /// <summary>
    /// FaturalamaDurumu'nun "muhasebe onaylandı, fatura kesimi bekleniyor/uygulanamaz" değerini
    /// belge tipine göre üretir - yalnızca STYS tarafından düzenlenen giden belgeler için
    /// KesimBekliyor, diğer TÜM tipler için Uygulanamaz döner.
    /// </summary>
    public static TicariBelgeFaturalamaDurumu ProjeOnaylandiFaturalamaDurumu(SatisBelgesiTipi belgeTipi)
        => belgeTipi.StysTarafindanDuzenlenirMi()
            ? TicariBelgeFaturalamaDurumu.KesimBekliyor
            : TicariBelgeFaturalamaDurumu.Uygulanamaz;

    // ═════════════════════════════════════════════════════════════
    // LEGACY/UYUMLULUK YÖNÜ — yalnızca migration + eski veri + geriye dönük projection testleri
    // (ÜRETİM KARAR MEKANİZMASI DEĞİLDİR)
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// [YALNIZCA LEGACY/MIGRATION/TEST] Ticari (hazırlık) durumu: Taslak ve IptalEdildi kendi
    /// karşılıklarına eşlenir; MUHASEBE REDDİ dahil diğer TÜM mevcut durumlar Hazir'e eşlenir -
    /// muhasebe reddi ticari belgenin kendisinin reddi SAYILMAZ (bkz. TicariBelgeDurumu doc'u); bu
    /// yüzden burada bir "Reddedildi" değeri YOKTUR.
    /// </summary>
    public static TicariBelgeDurumu ProjeTicariDurum(SatisBelgesiDurumu durum) => durum switch
    {
        SatisBelgesiDurumu.Taslak => TicariBelgeDurumu.Taslak,
        SatisBelgesiDurumu.IptalEdildi => TicariBelgeDurumu.IptalEdildi,
        _ => TicariBelgeDurumu.Hazir
    };

    /// <summary>
    /// [YALNIZCA LEGACY/MIGRATION/TEST] Muhasebeleştirme durumu: SatisBelgesiDurumu'nun TÜM (7)
    /// değeri açıkça eşlenir - bilinmeyen/gelecekte eklenecek bir değer SESSİZCE bir varsayılana
    /// düşürülmez, fail-closed olarak ArgumentOutOfRangeException fırlatılır.
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
    /// [YALNIZCA LEGACY/MIGRATION/TEST] Faturalama/gönderim durumu: ÖNCELİKLE mevcut durum
    /// geçmişi (FaturaKesildi/MusteriyeGonderildi/IptalEdildi) belge tipinden BAĞIMSIZ olarak
    /// korunur. Bunların dışında: yalnızca STYS tarafından düzenlenen giden belgeler için
    /// faturalama süreci "uygulanabilir" sayılır; diğer TÜM tipler her zaman Uygulanamaz'dır -
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

    /// <summary>[YALNIZCA LEGACY/MIGRATION/TEST] Üç projeksiyonu (eski BelgeTipi+Durum çiftinden) tek çağrıda üretir.</summary>
    public static (TicariBelgeDurumu TicariDurum, TicariBelgeMuhasebeDurumu MuhasebeDurumu, TicariBelgeFaturalamaDurumu FaturalamaDurumu) Proje(
        SatisBelgesiTipi belgeTipi, SatisBelgesiDurumu durum)
        => (ProjeTicariDurum(durum), ProjeMuhasebeDurumu(durum), ProjeFaturalamaDurumu(belgeTipi, durum));

    /// <summary>[YALNIZCA LEGACY/MIGRATION/TEST] Zaten elde bir SatisBelgesi entity'si varsa, üç alanı belgenin MEVCUT (eski) Durum'undan DOĞRUDAN üzerine yazar.</summary>
    public static void UygulaVeYaz(SatisBelgesi belge)
    {
        var (ticari, muhasebe, faturalama) = Proje(belge.BelgeTipi, belge.Durum);
        belge.TicariDurum = ticari;
        belge.MuhasebeDurumu = muhasebe;
        belge.FaturalamaDurumu = faturalama;
    }

    /// <summary>[YALNIZCA LEGACY/MIGRATION/TEST] belge.Durum'u yeniDurum'a atar ve ardından legacy→yeni projeksiyonu uygular.</summary>
    public static void DurumuAtaVeProjekteEt(SatisBelgesi belge, SatisBelgesiDurumu yeniDurum)
    {
        belge.Durum = yeniDurum;
        UygulaVeYaz(belge);
    }
}
