using STYS.Muhasebe.TasinirKartlari.Entities;

namespace STYS.Muhasebe.TasinirKartlari.Services;

internal static class TasinirKartServiceHelpers
{
    public static string ResolveTakipTipi(string? takipTipi, bool takipliMi)
    {
        var normalized = takipTipi?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return takipliMi ? TasinirKartTakipTipleri.Lot : TasinirKartTakipTipleri.Yok;
    }

    public static bool ResolveTakipliMi(string? takipTipi, bool takipliMi)
        => !string.Equals(ResolveTakipTipi(takipTipi, takipliMi), TasinirKartTakipTipleri.Yok, StringComparison.Ordinal);
}
