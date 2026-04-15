namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

public static class OdemeYontemleri
{
    public const string Nakit = "Nakit";
    public const string KrediKarti = "KrediKarti";
    public const string HavaleEft = "HavaleEft";
    public const string OdayaEkle = "OdayaEkle";
    public const string Mahsup = "Mahsup";

    public static readonly string[] Hepsi =
    [
        Nakit,
        KrediKarti,
        HavaleEft,
        OdayaEkle,
        Mahsup
    ];
}
