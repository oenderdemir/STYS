using STYS.Rezervasyonlar.Entities;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon icin cari kart cozumleme sirasini merkezilestiren servis.
/// Hem tahsilat (RezervasyonOdemeMuhasebeService) hem gelir tahakkuku
/// (RezervasyonGelirTahakkukService) tarafindan ortak kullanilir.
/// </summary>
public interface IRezervasyonCariKartResolver
{
    /// <summary>
    /// Cari kart cozumleme sirasi:
    ///   1) Rezervasyon.CariKartId onbellekte varsa kullan
    ///   2) Kullanici acikca bir cari kart secmisse (cariKartIdOverride) onu kullan (dogrulanir)
    ///   3) TCKN/VKN esleşmesi VEYA "guvenli" telefon esleşmesi (telefon + ad-soyad birlikte
    ///      esleşmeli — sadece telefonla eslesme aile bireylerini karistirabileceginden yetersizdir)
    ///      ile ayni tesiste mevcut bir Musteri/KurumsalMusteri cari kart varsa kullan
    ///   4) Tesisin konfigure edilmis "Rezervasyon Misafirleri" varsayilan cari karti varsa kullan
    ///   5) Hicbiri yoksa OTOMATIK CARI KART OLUSTURULMAZ — kullanicidan secim istenir
    ///      (RezervasyonOdemeMuhasebeService.CariKartSecimiGerekliStatusCode = 422)
    /// </summary>
    Task<int> ResolveAsync(Rezervasyon rezervasyon, int? cariKartIdOverride, CancellationToken cancellationToken = default);
}
