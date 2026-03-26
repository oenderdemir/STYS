namespace STYS.ErisimTeshis;

public sealed record ErisimTeshisModulTanimi(
    string Anahtar,
    string Ad,
    string Route,
    string MenuPermission,
    string ViewPermission,
    string ManagePermission,
    string ScopeTipi)
{
    public bool TesisScopeGerekli => string.Equals(ScopeTipi, ErisimTeshisScopeTipleri.Tesis, StringComparison.OrdinalIgnoreCase);

    public static ErisimTeshisModulTanimi Genel(
        string anahtar,
        string ad,
        string route,
        string menuPermission,
        string viewPermission,
        string managePermission)
    {
        return new ErisimTeshisModulTanimi(anahtar, ad, route, menuPermission, viewPermission, managePermission, ErisimTeshisScopeTipleri.Yok);
    }

    public static ErisimTeshisModulTanimi TesisScoped(
        string anahtar,
        string ad,
        string route,
        string menuPermission,
        string viewPermission,
        string managePermission)
    {
        return new ErisimTeshisModulTanimi(anahtar, ad, route, menuPermission, viewPermission, managePermission, ErisimTeshisScopeTipleri.Tesis);
    }
}
