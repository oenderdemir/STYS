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
