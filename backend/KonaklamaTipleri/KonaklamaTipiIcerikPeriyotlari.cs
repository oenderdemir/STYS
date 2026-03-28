namespace STYS.KonaklamaTipleri;

public static class KonaklamaTipiIcerikPeriyotlari
{
    public const string Gunluk = "Gunluk";
    public const string KonaklamaBoyunca = "KonaklamaBoyunca";

    public static readonly IReadOnlyDictionary<string, string> Adlar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Gunluk] = "Gunluk",
        [KonaklamaBoyunca] = "Konaklama Boyunca"
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && Adlar.ContainsKey(value);

    public static string GetAd(string? value)
        => value is not null && Adlar.TryGetValue(value, out var ad)
            ? ad
            : value ?? string.Empty;
}
