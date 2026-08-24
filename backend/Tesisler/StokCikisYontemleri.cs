namespace STYS.Tesisler;

public static class StokCikisYontemleri
{
    public const string TalepVeOnay = "TalepVeOnay";
    public const string DogrudanDepoCikisi = "DogrudanDepoCikisi";

    public static bool IsValid(string? value)
        => string.Equals(value, TalepVeOnay, StringComparison.Ordinal)
            || string.Equals(value, DogrudanDepoCikisi, StringComparison.Ordinal);
}
