using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Repositories;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Security.Auth.Services;
using BaseUserService = TOD.Platform.Identity.Users.Services.UserService;

namespace STYS.Kullanicilar.Services;

public class StysScopedUserService : BaseUserService
{
    private static readonly string[] AssignableMarkerPermissions =
    [
        StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir,
        StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir,
        StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir
    ];
    private static readonly string[] AssignableMarkerRoleNames =
    [
        nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir),
        nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir)
    ];

    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IAccessScopeProvider _accessScopeProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public StysScopedUserService(
        IUserRepository userRepository,
        IUserGroupRepository userGroupRepository,
        IPasswordHasher passwordHasher,
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        IAccessScopeProvider accessScopeProvider,
        ICurrentUserAccessor currentUserAccessor,
        AutoMapper.IMapper mapper)
        : base(userRepository, userGroupRepository, passwordHasher, mapper)
    {
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _accessScopeProvider = accessScopeProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    public override async Task<IEnumerable<UserDto>> GetAllAsync(Func<IQueryable<User>, IQueryable<User>>? include = null)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();

        var query = include is null
            ? Repository.Where(_ => true)
            : include(Repository.Where(_ => true));

        if (actorScope.IsTesisManagerScoped)
        {
            var manageableUserIds = await GetManageableUserIdsForScopedManagerAsync(actorScope);
            if (manageableUserIds.Count == 0)
            {
                return [];
            }

            query = query.Where(x => manageableUserIds.Contains(x.Id));
        }

        var users = await query.ToListAsync();
        return Mapper.Map<IEnumerable<UserDto>>(users);
    }

    public override async Task<UserDto?> GetByIdAsync(Guid id, Func<IQueryable<User>, IQueryable<User>>? include = null)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped && !actorScope.VisibleUserIds.Contains(id))
        {
            return null;
        }

        var query = include is null
            ? Repository.Where(x => x.Id == id)
            : include(Repository.Where(x => x.Id == id));

        var entity = await query.FirstOrDefaultAsync();
        return Mapper.Map<UserDto?>(entity);
    }

    public override async Task<UserDto> AddAsync(UserDto dto)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await ValidateScopedManagerGroupSelectionAsync(dto);
        }

        var created = await base.AddAsync(dto);

        if (!created.Id.HasValue)
        {
            return created;
        }

        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureOwnerRecordForScopedCreateAsync(created.Id.Value, actorScope, CancellationToken.None);
        }
        else
        {
            await EnsureOwnerRecordForUnscopedCreateAsync(created.Id.Value, CancellationToken.None);
        }

        return created;
    }

    public override async Task<UserDto> UpdateAsync(UserDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id cannot be empty.");
        }

        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(dto.Id.Value, actorScope);
            await ValidateScopedManagerGroupSelectionAsync(dto);
        }

        return await base.UpdateAsync(dto);
    }

    public override async Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(id, actorScope);
        }

        await base.ResetPasswordAsync(id, dto);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(id, actorScope);
        }

        await base.DeleteAsync(id);

        var ownerRows = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.UserId == id)
            .ToListAsync();

        if (ownerRows.Count > 0)
        {
            _stysDbContext.KullaniciTesisSahiplikleri.RemoveRange(ownerRows);
            await _stysDbContext.SaveChangesAsync();
        }
    }

    private async Task EnsureScopedManagerCanManageUserAsync(
        Guid targetUserId,
        UserActorScope actorScope,
        CancellationToken cancellationToken = default)
    {
        if (!actorScope.IsTesisManagerScoped)
        {
            return;
        }

        var actorPermissions = await GetCurrentUserPermissionSetAsync(cancellationToken);
        var targetAssignableMarkers = await GetTargetUserAssignableMarkersAsync(targetUserId, cancellationToken);
        if (targetAssignableMarkers.Count == 0)
        {
            throw new InvalidOperationException("Bu kullanici scoped yonetim kapsamindaki gruplarda degil.");
        }

        var requiredActorPermissions = targetAssignableMarkers
            .Select(MapAssignableMarkerToActorPermission)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingPermissions = requiredActorPermissions
            .Where(permission => !HasPermission(actorPermissions, permission))
            .ToList();

        if (missingPermissions.Count > 0)
        {
            throw new InvalidOperationException("Bu kullanici grubunu yonetmek icin gerekli atama yetkisine sahip degilsiniz.");
        }

        var ownerTesisId = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.UserId == targetUserId)
            .Select(x => x.TesisId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ownerTesisId.HasValue)
        {
            throw new InvalidOperationException("Bu kullanici sahipsiz oldugu icin global kullanici bilgileri tesis yoneticisi tarafindan degistirilemez.");
        }

        if (!actorScope.ManagedTesisIds.Contains(ownerTesisId.Value))
        {
            throw new InvalidOperationException("Bu kullanicinin global bilgilerini duzenleme yetkiniz yok.");
        }

        if (!actorScope.VisibleUserIds.Contains(targetUserId))
        {
            throw new InvalidOperationException("Bu kullaniciyi yonetme yetkiniz bulunmuyor.");
        }
    }

    private async Task ValidateScopedManagerGroupSelectionAsync(UserDto dto, CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetCurrentUserPermissionSetAsync(cancellationToken);
        var manageableGroupPermissionMap = await GetManageableGroupPermissionMapAsync(cancellationToken);

        var requestedGroupIds = dto.UserGroups
            .Select(x => x.Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (requestedGroupIds.Count == 0)
        {
            var defaultGroupId = manageableGroupPermissionMap
                .Where(x => HasPermission(actorPermissions, x.Value))
                .OrderByDescending(x => string.Equals(x.Value, StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key)
                .FirstOrDefault();

            if (defaultGroupId == Guid.Empty)
            {
                throw new InvalidOperationException("Kullanici atama yetkisi bulunmuyor.");
            }

            dto.UserGroups = [new UserGroupDto { Id = defaultGroupId }];
            return;
        }

        foreach (var groupId in requestedGroupIds)
        {
            if (!manageableGroupPermissionMap.TryGetValue(groupId, out var requiredPermission))
            {
                throw new InvalidOperationException("Scoped yonetici yalnizca yonetim grubu tipindeki kullanici gruplarina atama yapabilir.");
            }

            if (!HasPermission(actorPermissions, requiredPermission))
            {
                throw new InvalidOperationException("Secilen grup icin kullanici atama yetkiniz yok.");
            }
        }
    }

    private async Task EnsureOwnerRecordForScopedCreateAsync(
        Guid userId,
        UserActorScope actorScope,
        CancellationToken cancellationToken)
    {
        if (actorScope.ManagedTesisIds.Count == 0)
        {
            return;
        }

        var ownerTesisId = actorScope.ManagedTesisIds.Min();
        var existingOwner = await _stysDbContext.KullaniciTesisSahiplikleri
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existingOwner is null)
        {
            await _stysDbContext.KullaniciTesisSahiplikleri.AddAsync(new()
            {
                UserId = userId,
                TesisId = ownerTesisId
            }, cancellationToken);
            await _stysDbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!existingOwner.TesisId.HasValue)
        {
            existingOwner.TesisId = ownerTesisId;
            _stysDbContext.KullaniciTesisSahiplikleri.Update(existingOwner);
            await _stysDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureOwnerRecordForUnscopedCreateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var existingOwner = await _stysDbContext.KullaniciTesisSahiplikleri
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existingOwner is not null)
        {
            return;
        }

        await _stysDbContext.KullaniciTesisSahiplikleri.AddAsync(new()
        {
            UserId = userId,
            TesisId = null
        }, cancellationToken);
        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetManageableUserIdsForScopedManagerAsync(
        UserActorScope actorScope,
        CancellationToken cancellationToken = default)
    {
        if (!actorScope.IsTesisManagerScoped || actorScope.ManagedTesisIds.Count == 0)
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

    private async Task<Dictionary<Guid, string>> GetManageableGroupPermissionMapAsync(CancellationToken cancellationToken)
    {
        var groupRoleRows = await _identityDbContext.UserGroupRoles
            .Select(x => new
            {
                x.UserGroupId,
                x.Role.Domain,
                x.Role.Name
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, string>();

        foreach (var groupRoleRow in groupRoleRows.GroupBy(x => x.UserGroupId))
        {
            var markerPermission = groupRoleRow
                .Select(x => ToPermission(x.Domain, x.Name))
                .FirstOrDefault(IsAssignableMarkerPermission);

            if (string.IsNullOrWhiteSpace(markerPermission))
            {
                continue;
            }

            result[groupRoleRow.Key] = MapAssignableMarkerToActorPermission(markerPermission);
        }

        return result;
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

        var permissions = permissionRows
            .Select(x => ToPermission(x.Domain, x.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return permissions;
    }

    private async Task<List<string>> GetTargetUserAssignableMarkersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var permissionRows = await _identityDbContext.UserUserGroups
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.UserGroup.UserGroupRoles.Select(ugr => new { ugr.Role.Domain, ugr.Role.Name }))
            .Distinct()
            .ToListAsync(cancellationToken);

        var userPermissions = permissionRows
            .Select(x => ToPermission(x.Domain, x.Name))
            .ToList();

        var markers = userPermissions
            .Where(IsAssignableMarkerPermission)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (markers.Count > 0)
        {
            return markers;
        }

        return [];
    }

    private static bool HasPermission(IReadOnlySet<string> permissionSet, string permission)
    {
        return permissionSet.Contains(permission);
    }

    private static bool IsAssignableMarkerPermission(string permission)
    {
        return AssignableMarkerPermissions.Any(x => string.Equals(x, permission, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToPermission(string domain, string name)
    {
        return $"{domain}.{name}";
    }

    private static string MapAssignableMarkerToActorPermission(string markerPermission)
    {
        if (string.Equals(markerPermission, StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir, StringComparison.OrdinalIgnoreCase))
        {
            return StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir;
        }

        if (string.Equals(markerPermission, StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir, StringComparison.OrdinalIgnoreCase))
        {
            return StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir;
        }

        if (string.Equals(markerPermission, StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir, StringComparison.OrdinalIgnoreCase))
        {
            return StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir;
        }

        throw new InvalidOperationException("Desteklenmeyen atanabilir grup marker izni.");
    }

    private static HashSet<string> GetManageableAssignableMarkerRoleNames(IReadOnlySet<string> actorPermissions)
    {
        var manageableMarkerRoleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (HasPermission(actorPermissions, StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir))
        {
            manageableMarkerRoleNames.Add(nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir));
        }

        if (HasPermission(actorPermissions, StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir))
        {
            manageableMarkerRoleNames.Add(nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir));
        }

        if (HasPermission(actorPermissions, StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir))
        {
            manageableMarkerRoleNames.Add(nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        }

        return manageableMarkerRoleNames;
    }
}
