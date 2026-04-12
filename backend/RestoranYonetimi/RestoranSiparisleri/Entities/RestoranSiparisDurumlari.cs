namespace STYS.RestoranSiparisleri.Entities;

public static class RestoranSiparisDurumlari
{
    public const string Taslak = "Taslak";
    public const string Hazirlaniyor = "Hazirlaniyor";
    public const string Hazir = "Hazir";
    public const string Serviste = "Serviste";
    public const string Tamamlandi = "Tamamlandi";
    public const string Iptal = "Iptal";

    public static readonly IReadOnlyCollection<string> AcikSiparisDurumlari =
    [
        Taslak,
        Hazirlaniyor,
        Hazir,
        Serviste
    ];
}
