using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Kullanicilar.Services;

public class ManageableUserScopeService : IManageableUserScopeService
{
    private static readonly string[] AssignableMarkerRoleNames =
    [
        nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.MuhasebeciAtanabilir)
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IAccessScopeProvider _accessScopeProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public ManageableUserScopeService(
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        IAccessScopeProvider accessScopeProvider,
        ICurrentUserAccessor currentUserAccessor,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _accessScopeProvider = accessScopeProvider;
        _currentUserAccessor = currentUserAccessor;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public async Task<IReadOnlySet<Guid>?> GetManageableUserIdsAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return null;
        }

        if (_currentTenantAccessor.IsKurumAdmin())
        {
            return await GetCurrentKurumUserIdsAsync(cancellationToken);
        }

        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync(cancellationToken);

        if (!actorScope.IsTesisManagerScoped)
        {
            return null;
        }

        if (actorScope.ManagedTesisIds.Count == 0)
        {
            return null;
        }

        return await GetScopedManageableUserIdsAsync(actorScope, cancellationToken);
    }

    public async Task<bool> CanManageUserAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var manageableIds = await GetManageableUserIdsAsync(cancellationToken);
        return manageableIds is null || manageableIds.Contains(targetUserId);
    }

    private async Task<HashSet<Guid>> GetCurrentKurumUserIdsAsync(CancellationToken cancellationToken)
    {
        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
        if (!currentKurumId.HasValue)
        {
            return [];
        }

        return await _identityDbContext.UserKurums
            .Where(x => x.KurumId == currentKurumId.Value && x.AktifMi && !x.IsDeleted)
            .Select(x => x.UserId)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetScopedManageableUserIdsAsync(
        UserActorScope actorScope,
        CancellationToken cancellationToken)
    {
        if (actorScope.ManagedTesisIds.Count == 0)
        {
            return [];
        }

        var ownerScopedUserIds = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.TesisId.HasValue && actorScope.ManagedTesisIds.Contains(x.TesisId.Value))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (ownerScopedUserIds.Count == 0)
        {
            return [];
        }

        var actorPermissions = await GetCurrentUserPermissionSetAsync(cancellationToken);
        var manageableMarkerRoleNames = GetManageableAssignableMarkerRoleNames(actorPermissions);
        if (manageableMarkerRoleNames.Count == 0)
        {
            return [];
        }

        var markerRows = await _identityDbContext.UserUserGroups
            .Where(x => ownerScopedUserIds.Contains(x.UserId))
            .SelectMany(x => x.UserGroup.UserGroupRoles
                .Where(ugr =>
                    ugr.Role.Domain == nameof(StructurePermissions.KullaniciAtama)
                    && AssignableMarkerRoleNames.Contains(ugr.Role.Name))
                .Select(ugr => new
                {
                    x.UserId,
                    MarkerRoleName = ugr.Role.Name
                }))
            .Distinct()
            .ToListAsync(cancellationToken);

        var markersByUserId = markerRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.MarkerRoleName).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var manageableUserIds = new HashSet<Guid>();
        foreach (var userId in ownerScopedUserIds)
        {
            if (!markersByUserId.TryGetValue(userId, out var markerRoleNames) || markerRoleNames.Count == 0)
            {
                continue;
            }

            if (markerRoleNames.All(markerRoleName => manageableMarkerRoleNames.Contains(markerRoleName)))
            {
                manageableUserIds.Add(userId);
            }
        }

        return manageableUserIds;
    }

    private async Task<HashSet<string>> GetCurrentUserPermissionSetAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserAccessor.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var permissionRows = await _identityDbContext.UserUserGroups
            .Where(x => x.UserId == userId.Value)
            .SelectMany(x => x.UserGroup.UserGroupRoles.Select(ugr => new { ugr.Role.Domain, ugr.Role.Name }))
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissionRows
            .Select(x => $"{x.Domain}.{x.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetManageableAssignableMarkerRoleNames(IReadOnlySet<string> actorPermissions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir));

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir));

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir));

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranGarsonuAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir));

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));

        if (actorPermissions.Contains(StructurePermissions.KullaniciAtama.MuhasebeciAtayabilir))
            result.Add(nameof(StructurePermissions.KullaniciAtama.MuhasebeciAtanabilir));

        return result;
    }
}
