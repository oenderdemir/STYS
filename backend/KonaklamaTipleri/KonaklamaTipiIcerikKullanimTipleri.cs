namespace STYS.KonaklamaTipleri;

public static class KonaklamaTipiIcerikKullanimTipleri
{
    public const string Adetli = "Adetli";
    public const string Sinirsiz = "Sinirsiz";

    public static readonly IReadOnlyDictionary<string, string> Adlar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Adetli] = "Adetli",
        [Sinirsiz] = "Sinirsiz"
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && Adlar.ContainsKey(value);

    public static string GetAd(string? value)
        => value is not null && Adlar.TryGetValue(value, out var ad)
            ? ad
            : value ?? string.Empty;
}
