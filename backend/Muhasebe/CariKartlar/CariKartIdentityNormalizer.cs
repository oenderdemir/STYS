using STYS.Muhasebe.CariKartlar.Entities;

namespace STYS.Muhasebe.CariKartlar;

public static class CariKartIdentityNormalizer
{
    public static string? NormalizeVergiNoTckn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var chars = new char[trimmed.Length];
        var index = 0;

        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch) || ch is '-' or '.')
            {
                continue;
            }

            chars[index++] = char.ToUpperInvariant(ch);
        }

        if (index == 0)
        {
            return null;
        }

        var normalized = new string(chars, 0, index);
        return normalized.Length > 32 ? normalized[..32] : normalized;
    }

    public static bool IsMusteriGrubu(string? cariTipi)
        => string.Equals(cariTipi, CariKartTipleri.Musteri, StringComparison.OrdinalIgnoreCase)
            || string.Equals(cariTipi, CariKartTipleri.KurumsalMusteri, StringComparison.OrdinalIgnoreCase);
}
