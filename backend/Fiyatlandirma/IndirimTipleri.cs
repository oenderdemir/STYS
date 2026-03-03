namespace STYS.Fiyatlandirma;

public static class IndirimTipleri
{
    public const string Yuzde = "Yuzde";
    public const string Tutar = "Tutar";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Yuzde,
        Tutar
    };
}
