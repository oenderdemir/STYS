namespace STYS.Fiyatlandirma;

public static class OdaFiyatKullanimSekilleri
{
    public const string KisiBasi = "KisiBasi";
    public const string OzelKullanim = "OzelKullanim";

    public static readonly IReadOnlySet<string> Tumu = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        KisiBasi,
        OzelKullanim
    };
}
