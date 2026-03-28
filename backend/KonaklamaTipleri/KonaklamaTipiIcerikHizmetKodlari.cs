namespace STYS.KonaklamaTipleri;

public static class KonaklamaTipiIcerikHizmetKodlari
{
    public const string Kahvalti = "Kahvalti";
    public const string OgleYemegi = "OgleYemegi";
    public const string AksamYemegi = "AksamYemegi";
    public const string Wifi = "Wifi";
    public const string Otopark = "Otopark";
    public const string HavaalaniTransferi = "HavaalaniTransferi";
    public const string GunlukTemizlik = "GunlukTemizlik";

    public static readonly IReadOnlyDictionary<string, string> Adlar = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Kahvalti] = "Kahvalti",
        [OgleYemegi] = "Ogle Yemegi",
        [AksamYemegi] = "Aksam Yemegi",
        [Wifi] = "Wi-Fi",
        [Otopark] = "Otopark",
        [HavaalaniTransferi] = "Havaalani Transferi",
        [GunlukTemizlik] = "Gunluk Temizlik"
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && Adlar.ContainsKey(value);

    public static string GetAd(string? value)
        => value is not null && Adlar.TryGetValue(value, out var ad)
            ? ad
            : value ?? string.Empty;
}
