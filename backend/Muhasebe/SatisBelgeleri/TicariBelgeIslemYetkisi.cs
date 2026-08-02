using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Ticari belge (SatisBelgesi) üzerindeki operasyonel işlem yetkilerini (güncellenebilir mi,
/// silinebilir mi, muhasebe onayına gönderilebilir mi, iptal edilebilir mi) ÜÇ OTORİTER durumdan
/// (TicariDurum, MuhasebeDurumu, FaturalamaDurumu) türeten, saf ve tek merkezi karar kaynağı.
/// Legacy SatisBelgesiDurumu ASLA kullanılmaz (bkz. SatisBelgesiDurumProjection).
///
/// Hem SatisBelgesiService (Muhasebe.SatisBelgeleri) hem TicariBelgeService (TicariBelgeler,
/// operasyon uygulama katmanı) BU sınıfı kullanır — aynı kural iki yerde ayrı ayrı YENİDEN
/// UYGULANMAZ. Bilinçli olarak Muhasebe.SatisBelgeleri modülünde tutulur (TicariBelgeDurumu vb.
/// enumların da ait olduğu yer) — böylece TicariBelgeler modülü (zaten izin verilen yönde) buraya
/// bağımlı olabilirken, Muhasebe modülünün TicariBelgeler'e bağımlı olması hiçbir zaman gerekmez.
/// </summary>
public static class TicariBelgeIslemYetkisi
{
    /// <summary>Taslak durumunda YA DA muhasebece reddedilmiş bir belge güncellenebilir.</summary>
    public static bool GuncellenebilirMi(TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu)
        => ticariDurum == TicariBelgeDurumu.Taslak
           || muhasebeDurumu == TicariBelgeMuhasebeDurumu.Reddedildi;

    /// <summary>
    /// Yalnızca GERÇEK taslak kombinasyonu (TicariDurum=Taslak + MuhasebeDurumu=Bekliyor +
    /// FaturalamaDurumu=Baslatilmadi/Uygulanamaz) silinebilir.
    /// </summary>
    public static bool SilinebilirMi(
        TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu, TicariBelgeFaturalamaDurumu faturalamaDurumu)
        => ticariDurum == TicariBelgeDurumu.Taslak
           && muhasebeDurumu == TicariBelgeMuhasebeDurumu.Bekliyor
           && (faturalamaDurumu == TicariBelgeFaturalamaDurumu.Baslatilmadi
               || faturalamaDurumu == TicariBelgeFaturalamaDurumu.Uygulanamaz);

    /// <summary>Yalnızca TicariDurum=Taslak VE MuhasebeDurumu=Bekliyor olan bir belge muhasebe onayına gönderilebilir.</summary>
    public static bool MuhasebeOnayinaGonderilebilirMi(TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu)
        => ticariDurum == TicariBelgeDurumu.Taslak
           && muhasebeDurumu == TicariBelgeMuhasebeDurumu.Bekliyor;

    /// <summary>
    /// Zaten iptal edilmiş (TicariDurum=IptalEdildi) YA DA resmî faturası kesilmiş/müşteriye
    /// gönderilmiş (FaturalamaDurumu=Kesildi/MusteriyeGonderildi) bir belge iptal edilemez.
    /// </summary>
    public static bool IptalEdilebilirMi(TicariBelgeDurumu ticariDurum, TicariBelgeFaturalamaDurumu faturalamaDurumu)
        => ticariDurum != TicariBelgeDurumu.IptalEdildi
           && faturalamaDurumu != TicariBelgeFaturalamaDurumu.Kesildi
           && faturalamaDurumu != TicariBelgeFaturalamaDurumu.MusteriyeGonderildi;

    /// <summary>Yalnızca MuhasebeDurumu=Onayda olan (ve iptal edilmemiş) bir belge muhasebece onaylanabilir.</summary>
    public static bool MuhasebeOnaylanabilirMi(TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu)
        => ticariDurum != TicariBelgeDurumu.IptalEdildi
           && muhasebeDurumu == TicariBelgeMuhasebeDurumu.Onayda;

    /// <summary>Yalnızca MuhasebeDurumu=Onayda olan (ve iptal edilmemiş) bir belge reddedilebilir.</summary>
    public static bool ReddedilebilirMi(TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu)
        => ticariDurum != TicariBelgeDurumu.IptalEdildi
           && muhasebeDurumu == TicariBelgeMuhasebeDurumu.Onayda;

    /// <summary>
    /// Belgenin ARTIK bir mali etki doğurup doğurmadığını belirler: bağlı bir muhasebe fişi
    /// (MuhasebeFisId) VARSA, YA DA muhasebe onayı verilmişse (MuhasebeDurumu=Onaylandi) — henüz
    /// fiş oluşturulmamış olsa bile — belge mali açıdan "gerçekleşmiş" sayılır. Bu tek kaynak,
    /// operasyon sınırının (bkz. görev 2 - ui/ticari-belgeler) mali etkisi doğmuş bir belgeyi
    /// ASLA iptal edememesini garanti eder; muhasebe tarafı (SatisBelgesiService.IptalEtAsync +
    /// bağlı MuhasebeFisiIptalEtAsync ters kayıt akışı) bu belgeleri YİNE DE iptal edebilir - bu
    /// metot yalnızca OPERASYON sınırı için kullanılır, muhasebe tarafının kendi iptal kuralı
    /// (bkz. IptalEdilebilirMi) DEĞİŞTİRİLMEZ.
    /// </summary>
    public static bool MaliEtkisiOlusmusMu(TicariBelgeMuhasebeDurumu muhasebeDurumu, int? muhasebeFisId)
        => muhasebeFisId.HasValue || muhasebeDurumu == TicariBelgeMuhasebeDurumu.Onaylandi;

    /// <summary>
    /// Operasyon sınırı (ui/ticari-belgeler) için iptal edilebilirlik: temel IptalEdilebilirMi
    /// kuralına EK olarak, mali etkisi doğmuş (bkz. MaliEtkisiOlusmusMu) bir belge operasyon
    /// ekranından ASLA iptal edilemez - bu belgeler yalnızca Muhasebe Satış/Alış Belgeleri
    /// ekranından (MuhasebeSatisBelgeleriYonetimi.Manage) iptal edilebilir.
    /// </summary>
    public static bool OperasyonelIptalEdilebilirMi(
        TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu, TicariBelgeFaturalamaDurumu faturalamaDurumu, int? muhasebeFisId)
        => IptalEdilebilirMi(ticariDurum, faturalamaDurumu) && !MaliEtkisiOlusmusMu(muhasebeDurumu, muhasebeFisId);

    /// <summary>
    /// Muhasebe fişi YALNIZCA MuhasebeDurumu=Onaylandi VE henüz bağlı bir MuhasebeFisId yokken
    /// oluşturulabilir. Belge tipi bir İZİN LİSTESİYLE (allowlist) doğrulanır — YALNIZCA
    /// SatisFaturasi, AlisFaturasi, SatisIadeFaturasi ve AlisIadeFaturasi desteklenir.
    /// FaturaTaslagi, Proforma, legacy IadeFaturasi VE tanımsız/gelecekte eklenecek herhangi bir
    /// enum değeri (fail-closed - blocklist DEĞİL, allowlist) HER ZAMAN false döner.
    /// </summary>
    public static bool MuhasebeFisiOlusturulabilirMi(
        TicariBelgeMuhasebeDurumu muhasebeDurumu, int? muhasebeFisId, SatisBelgesiTipi belgeTipi)
    {
        if (muhasebeDurumu != TicariBelgeMuhasebeDurumu.Onaylandi || muhasebeFisId.HasValue)
            return false;

        return belgeTipi is SatisBelgesiTipi.SatisFaturasi
            or SatisBelgesiTipi.AlisFaturasi
            or SatisBelgesiTipi.SatisIadeFaturasi
            or SatisBelgesiTipi.AlisIadeFaturasi;
    }

    /// <summary>
    /// Üç otoriter durumdan operasyon personeline gösterilecek KISA, insan-okur bir Türkçe durum
    /// açıklaması üretir. Legacy SatisBelgesiDurumu'nun yerine kullanılmak üzere tasarlanmıştır
    /// (bkz. RezervasyonGelirTahakkukService) — legacy enum adlarını YANSITMAZ, yalnızca operasyon
    /// personelinin ihtiyaç duyduğu düzeyde bir özet sunar.
    /// </summary>
    public static string OperasyonelDurumAciklamasi(
        TicariBelgeDurumu ticariDurum, TicariBelgeMuhasebeDurumu muhasebeDurumu, TicariBelgeFaturalamaDurumu faturalamaDurumu)
    {
        if (ticariDurum == TicariBelgeDurumu.IptalEdildi)
        {
            return "İptal edildi";
        }

        if (faturalamaDurumu == TicariBelgeFaturalamaDurumu.MusteriyeGonderildi)
        {
            return "Fatura kesildi ve müşteriye gönderildi";
        }

        if (faturalamaDurumu == TicariBelgeFaturalamaDurumu.Kesildi)
        {
            return "Fatura kesildi";
        }

        return muhasebeDurumu switch
        {
            TicariBelgeMuhasebeDurumu.Bekliyor => "Taslak",
            TicariBelgeMuhasebeDurumu.Onayda => "Muhasebe onayında",
            TicariBelgeMuhasebeDurumu.Onaylandi => "Muhasebe onaylandı",
            TicariBelgeMuhasebeDurumu.Reddedildi => "Muhasebe tarafından reddedildi",
            _ => muhasebeDurumu.ToString()
        };
    }
}
