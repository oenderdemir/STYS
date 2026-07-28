namespace STYS.Rezervasyonlar;

public static class RezervasyonUzatmaSenaryoTipleri
{
    /// <summary>Mevcut son segmentteki oda dagilimi, uzatma araliginin tamaminda degismeden kullanilabiliyor.</summary>
    public const string AyniOdadaDevam = "AyniOdadaDevam";

    /// <summary>Mevcut cikis tarihi aninda (uzatmanin basinda) tek seferlik oda degisimiyle, uzatma
    /// araliginin tamami tek bir oda dagilimiyla karsilanabiliyor.</summary>
    public const string CheckoutGunundeOdaDegisimi = "CheckoutGunundeOdaDegisimi";

    /// <summary>Uzatma araligi, gercek bir musaitlik sinirinda ikiye bolunerek en fazla bir oda
    /// degisikligi iceren iki segmentli bir planla karsilanabiliyor.</summary>
    public const string UzatmaSirasindaOdaDegisimi = "UzatmaSirasindaOdaDegisimi";
}
