namespace STYS.Rezervasyonlar;

/// <summary>
/// Rezervasyon gelir belgesi (fatura) olustuktan sonra, o rezervasyona ait
/// onceki tahsilatlarin faturanin cari hareketine karsi ne kadarinin
/// kapatildigini gosteren ekran-durumu. Kalici bir DB alani degildir —
/// RezervasyonGelirTahakkukService tarafindan istek aninda hesaplanir.
/// </summary>
public static class TahsilatKapamaDurumlari
{
    /// <summary>Fatura henuz yok, veya fatura var ama hic tahsilat kapamasi calisturulmamis.</summary>
    public const string Kapatilmadi = "Kapatilmadi";

    /// <summary>Bazi tahsilatlar kapatildi, ama toplam tahsilat tutari faturanin GenelToplam'ini
    /// karsilamiyor (orn. kapama islemi henuz tum belgeler icin calistirilmadi).</summary>
    public const string KismenKapatildi = "KismenKapatildi";

    /// <summary>Rezervasyona ait tum aktif tahsilatlar faturaya karsi kapatildi.</summary>
    public const string TamKapatildi = "TamKapatildi";

    /// <summary>Kapama denendi ama en az bir tahsilat icin basarisiz oldu (orn. donem kapali,
    /// tutar uyumsuzlugu). Elle mudahale/tekrar deneme gerektirir.</summary>
    public const string Hata = "Hata";
}
