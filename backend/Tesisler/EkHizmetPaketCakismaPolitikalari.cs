namespace STYS.Tesisler;

public static class EkHizmetPaketCakismaPolitikalari
{
    public const string Uyari = "Uyari";
    public const string OnayIste = "OnayIste";
    public const string Engelle = "Engelle";

    public static bool IsValid(string? value)
        => value is Uyari or OnayIste or Engelle;
}
