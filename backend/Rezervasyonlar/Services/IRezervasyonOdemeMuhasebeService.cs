using STYS.Rezervasyonlar.Entities;

namespace STYS.Rezervasyonlar.Services;

public interface IRezervasyonOdemeMuhasebeService
{
    /// <summary>
    /// Rezervasyon odemesi icin TahsilatOdemeBelgesi olusturur ve RezervasyonOdeme'ye baglar.
    /// Ambient transaction bekler — kendi transaction'ini ACMAZ; cagiran (RezervasyonService)
    /// RezervasyonOdeme ile ayni transaction icinde cagirmalidir (bkz. mimari revizyon #7/#8).
    /// Otomatik MuhasebeFis URETMEZ — fis uretimi ayri bir aksiyondur (ITahsilatOdemeBelgesiMuhasebeFisService).
    /// </summary>
    Task TahsilatOlusturAsync(
        Rezervasyon rezervasyon,
        RezervasyonOdeme odeme,
        int? kasaBankaHesapId,
        int? cariKartIdOverride,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Iptal edilen bir rezervasyon odemesinin muhasebe izini geri alir: bagli TahsilatOdemeBelgesi'ni
    /// iptal eder (varsa CariHareket'i ters cevirir) ve fis Onayli ise ters kayit acar.
    /// Ambient transaction bekler.
    /// </summary>
    Task TahsilatIptalEtAsync(
        RezervasyonOdeme odeme,
        string? iptalAciklama,
        CancellationToken cancellationToken = default);
}
