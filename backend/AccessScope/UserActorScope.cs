namespace STYS.AccessScope;

/// <summary>
/// Kullanıcının "hangi kullanıcılar üzerinde işlem yapabileceğini" tanımlar.
/// Bu scope; özellikle kullanıcı yönetimi (create/update/delete/view user) tarafında actor-based filtreleme için kullanılır.
/// DomainAccessScope'tan farkı, veri hiyerarşisini değil kullanıcı yönetim sorumluluk alanını taşımasıdır.
/// </summary>
public sealed class UserActorScope
{
    private UserActorScope(
        bool isTesisManagerScoped,
        HashSet<int> managedTesisIds,
        HashSet<int> managedBinaIds,
        HashSet<Guid> visibleUserIds)
    {
        IsTesisManagerScoped = isTesisManagerScoped;
        ManagedTesisIds = managedTesisIds;
        ManagedBinaIds = managedBinaIds;
        VisibleUserIds = visibleUserIds;
    }

    public bool IsTesisManagerScoped { get; }

    public IReadOnlySet<int> ManagedTesisIds { get; }

    public IReadOnlySet<int> ManagedBinaIds { get; }

    public IReadOnlySet<Guid> VisibleUserIds { get; }

    public static UserActorScope Unrestricted()
    {
        return new UserActorScope(false, [], [], []);
    }

    public static UserActorScope TesisManagerScoped(
        IEnumerable<int> managedTesisIds,
        IEnumerable<int> managedBinaIds,
        IEnumerable<Guid> visibleUserIds)
    {
        return new UserActorScope(
            true,
            managedTesisIds.ToHashSet(),
            managedBinaIds.ToHashSet(),
            visibleUserIds.ToHashSet());
    }
}
