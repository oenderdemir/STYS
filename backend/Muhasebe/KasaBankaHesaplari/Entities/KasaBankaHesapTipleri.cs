using System.Collections.Generic;

namespace STYS.Muhasebe.KasaBankaHesaplari.Entities;

public static class KasaBankaHesapTipleri
{
    public const string NakitKasa = "NakitKasa";
    public const string Banka = "Banka";

    public static readonly IReadOnlySet<string> TumTipler = new HashSet<string>(StringComparer.Ordinal)
    {
        NakitKasa,
        Banka
    };
}
