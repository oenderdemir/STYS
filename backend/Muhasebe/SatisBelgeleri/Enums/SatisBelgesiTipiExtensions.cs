namespace STYS.Muhasebe.SatisBelgeleri.Enums;

public static class SatisBelgesiTipiExtensions
{
    public static bool IsSatisBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.FaturaTaslagi
            or SatisBelgesiTipi.SatisFaturasi
            or SatisBelgesiTipi.SatisIadeFaturasi
            or SatisBelgesiTipi.IadeFaturasi
            or SatisBelgesiTipi.Proforma;

    public static bool IsAlisBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.AlisFaturasi
            or SatisBelgesiTipi.AlisIadeFaturasi;

    public static bool IsIadeBelgesi(this SatisBelgesiTipi belgeTipi)
        => belgeTipi is SatisBelgesiTipi.IadeFaturasi
            or SatisBelgesiTipi.SatisIadeFaturasi
            or SatisBelgesiTipi.AlisIadeFaturasi;
}
