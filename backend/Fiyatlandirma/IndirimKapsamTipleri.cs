namespace STYS.Fiyatlandirma;

public static class IndirimKapsamTipleri
{
    public const string Sistem = "Sistem";
    public const string Tesis = "Tesis";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Sistem,
        Tesis
    };
}
