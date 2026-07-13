namespace STYS.Rezervasyonlar;

public static class OdemeTipleri
{
    public const string Nakit = "Nakit";
    public const string KrediKarti = "KrediKarti";
    public const string HavaleEft = "HavaleEft";

    // Muhasebe.TahsilatOdemeBelgeleri.Entities.OdemeYontemleri ile ayni degerleri
    // kullanir (TahsilatOdemeBelgesiService.ValidateAsync bu degerlere karsi dogrular).
    public static readonly string[] Hepsi = [Nakit, KrediKarti, HavaleEft];
}

