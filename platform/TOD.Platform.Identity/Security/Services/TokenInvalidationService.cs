using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Infrastructure.EntityFramework;

namespace TOD.Platform.Identity.Security.Services;

public class TokenInvalidationService : ITokenInvalidationService
{
    private readonly TodIdentityDbContext _dbContext;

    public TokenInvalidationService(TodIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task InvalidateUserAsync(Guid userId, string reason, CancellationToken cancellationToken = default)
    {
        return InvalidateUsersAsync([userId], reason, cancellationToken);
    }

    public async Task InvalidateUsersAsync(IEnumerable<Guid> userIds, string reason, CancellationToken cancellationToken = default)
    {
        var distinctUserIds = userIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0)
        {
            return;
        }

        var users = await _dbContext.Users
            .Where(x => distinctUserIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return;
        }

        foreach (var user in users)
        {
            user.TokenVersion += 1;
        }

        var now = DateTime.UtcNow;
        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(x => distinctUserIds.Contains(x.UserId) && !x.RevokedAt.HasValue && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAt = now;
            refreshToken.RevokeReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateUsersByGroupIdsAsync(IEnumerable<Guid> userGroupIds, string reason, CancellationToken cancellationToken = default)
    {
        var distinctGroupIds = userGroupIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctGroupIds.Count == 0)
        {
            return;
        }

        var userIds = await _dbContext.UserUserGroups
            .Where(x => distinctGroupIds.Contains(x.UserGroupId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InvalidateUsersAsync(userIds, reason, cancellationToken);
    }

    public async Task InvalidateUsersByRoleIdsAsync(IEnumerable<Guid> roleIds, string reason, CancellationToken cancellationToken = default)
    {
        var distinctRoleIds = roleIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctRoleIds.Count == 0)
        {
            return;
        }

        var groupIds = await _dbContext.UserGroupRoles
            .Where(x => distinctRoleIds.Contains(x.RoleId))
            .Select(x => x.UserGroupId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InvalidateUsersByGroupIdsAsync(groupIds, reason, cancellationToken);
    }
}
