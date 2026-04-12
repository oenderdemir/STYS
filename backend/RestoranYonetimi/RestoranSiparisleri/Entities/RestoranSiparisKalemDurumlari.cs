namespace STYS.RestoranSiparisleri.Entities;

public static class RestoranSiparisKalemDurumlari
{
    public const string Beklemede = "Beklemede";
    public const string Hazirlaniyor = "Hazirlaniyor";
    public const string Hazir = "Hazir";
    public const string ServisEdildi = "ServisEdildi";
    public const string Iptal = "Iptal";

    public static readonly IReadOnlyCollection<string> TumDurumlar =
    [
        Beklemede,
        Hazirlaniyor,
        Hazir,
        ServisEdildi,
        Iptal
    ];
}
