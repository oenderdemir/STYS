namespace STYS.Kullanicilar.Services;

public interface IManageableUserScopeService
{
    /// <summary>
    /// Returns null for unrestricted users (admin, kurum admin, non-scoped) meaning no filter should apply.
    /// Returns an empty set if the scoped user has no manageable users.
    /// Returns a non-empty set of user IDs the current scoped user can manage.
    /// </summary>
    Task<IReadOnlySet<Guid>?> GetManageableUserIdsAsync(CancellationToken cancellationToken = default);

    Task<bool> CanManageUserAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}
