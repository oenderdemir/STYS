namespace STYS.Muhasebe.CariKartlar.Entities;

public static class CariKartAcilisBakiyeYonleri
{
    public const string Borc = "Borc";
    public const string Alacak = "Alacak";

    public static readonly IReadOnlyCollection<string> Hepsi =
    [
        Borc,
        Alacak
    ];
}
