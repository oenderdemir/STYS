namespace STYS.KonaklamaTipleri;

public static class KonaklamaTipiIcerikKullanimNoktalari
{
    public const string Genel = "Genel";
    public const string Restoran = "Restoran";
    public const string Bar = "Bar";
    public const string OdaServisi = "OdaServisi";

    public static readonly IReadOnlyDictionary<string, string> Adlar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Genel] = "Genel",
        [Restoran] = "Restoran",
        [Bar] = "Bar",
        [OdaServisi] = "Oda Servisi"
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && Adlar.ContainsKey(value);

    public static string GetAd(string? value)
        => value is not null && Adlar.TryGetValue(value, out var ad)
            ? ad
            : value ?? string.Empty;
}
