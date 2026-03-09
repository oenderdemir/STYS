namespace TOD.Platform.Identity.Security.Services;

public interface ITokenInvalidationService
{
    Task InvalidateUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default);

    Task InvalidateUsersAsync(IEnumerable<Guid> userIds, string reason, CancellationToken cancellationToken = default);

    Task InvalidateUsersByGroupIdsAsync(IEnumerable<Guid> userGroupIds, string reason, CancellationToken cancellationToken = default);

    Task InvalidateUsersByRoleIdsAsync(IEnumerable<Guid> roleIds, string reason, CancellationToken cancellationToken = default);
}
