using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Rezervasyonlar.Dto;

namespace STYS.Rezervasyonlar.Services;

/// <summary>
/// Rezervasyon/konaklama gelir tahakkuku akisini orkestre eden servis. Kendi basina
/// satir/fis mantigi yazmaz — RezervasyonSatisBelgesiService (taslak), SatisBelgesiMuhasebeFisService
/// (fis, muhasebe ekraninin kendi akisindan) ve CariHareketKapamaService (kapama) uzerine ince bir
/// katmandir.
/// </summary>
public interface IRezervasyonGelirTahakkukService
{
    /// <summary>
    /// Gelir belgesi (SatisBelgesi) taslagini olusturur. Idempotenttir: Rezervasyon.SatisBelgesiId
    /// zaten doluysa yeni belge yaratmaz, mevcut belgeyi doner. Check-out akisi tarafindan
    /// best-effort cagrilir; hata durumunda cagiran taraf (RezervasyonService.TamamlaCheckOutAsync)
    /// check-out'u BASARISIZ SAYMAMALIDIR — bu servis kendi icinde check-out'u engellemez,
    /// sorumluluk cagiran taraftadir.
    /// </summary>
    Task<SatisBelgesiDto> OlusturTaslakAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rezervasyonun gelir ve tahsilat-kapama durumunu ekran icin ozetler.
    /// </summary>
    Task<RezervasyonGelirOzetiDto> GetGelirOzetiAsync(int rezervasyonId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rezervasyona ait, henuz kapatilmamis aktif tahsilatlari gelir belgesinin cari hareketine
    /// karsi kapatir. YALNIZCA satis belgesi onaylanip SatisBelgesi kaynakli CariHareket
    /// olustuktan sonra calisir — otomatik zincirlenmez, ayri bir "Tahsilatlari Kapat" aksiyonudur.
    /// </summary>
    Task<RezervasyonTahsilatKapamaSonucuDto> KapatOncekiTahsilatlariAsync(int rezervasyonId, CancellationToken cancellationToken = default);
}
