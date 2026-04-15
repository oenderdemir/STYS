namespace STYS.Muhasebe.CariKartlar.Entities;

public static class CariKartTipleri
{
    public const string Musteri = "Musteri";
    public const string Tedarikci = "Tedarikci";
    public const string KurumsalMusteri = "KurumsalMusteri";
    public const string Personel = "Personel";
    public const string Diger = "Diger";

    public static readonly IReadOnlyCollection<string> Hepsi =
    [
        Musteri,
        Tedarikci,
        KurumsalMusteri,
        Personel,
        Diger
    ];
}

