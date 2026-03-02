namespace STYS.AccessScope;

/// <summary>
/// Domain verisi filtreleme kapsamını taşır.
/// Bu scope; il, tesis ve bina listelerinde hangi kayıtların görülebileceğini belirlemek için kullanılır.
/// Kullanım amacı veri erişimidir (listeme/getir/sorgu filtreleri), kullanıcı CRUD yetki kapsamı değildir.
/// </summary>
public sealed class DomainAccessScope
{
    private DomainAccessScope(bool isScoped, HashSet<int> ilIds, HashSet<int> tesisIds, HashSet<int> binaIds)
    {
        IsScoped = isScoped;
        IlIds = ilIds;
        TesisIds = tesisIds;
        BinaIds = binaIds;
    }

    public bool IsScoped { get; }

    public IReadOnlySet<int> IlIds { get; }

    public IReadOnlySet<int> TesisIds { get; }

    public IReadOnlySet<int> BinaIds { get; }

    public static DomainAccessScope Unscoped()
    {
        return new DomainAccessScope(false, [], [], []);
    }

    public static DomainAccessScope Scoped(
        IEnumerable<int> ilIds,
        IEnumerable<int> tesisIds,
        IEnumerable<int> binaIds)
    {
        return new DomainAccessScope(
            true,
            ilIds.ToHashSet(),
            tesisIds.ToHashSet(),
            binaIds.ToHashSet());
    }
}
