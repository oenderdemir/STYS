using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;

namespace STYS.AccessScope;

/// <summary>
/// DomainAccessScope ve UserActorScope değerlerini request bazında bir kez hesaplar
/// ve aynı request içinde tekrar kullanım için cache'ler.
/// Böylece scope üretimi tek yerde kalır ve servisler yalnızca IAccessScopeProvider çağırır.
/// </summary>
public class AccessScopeProvider : IAccessScopeProvider
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private DomainAccessScope? _domainAccessScope;
    private UserActorScope? _userActorScope;

    public AccessScopeProvider(
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        ICurrentUserAccessor currentUserAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<DomainAccessScope> GetDomainAccessScopeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureScopesAsync(cancellationToken);
        return _domainAccessScope!;
    }

    public async Task<UserActorScope> GetUserActorScopeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureScopesAsync(cancellationToken);
        return _userActorScope!;
    }

    private async Task EnsureScopesAsync(CancellationToken cancellationToken)
    {
        if (_domainAccessScope is not null && _userActorScope is not null)
        {
            return;
        }

        if (IsCurrentUserAdmin())
        {
            _domainAccessScope = DomainAccessScope.Unscoped();
            _userActorScope = UserActorScope.Unrestricted();
            return;
        }

        var userId = _currentUserAccessor.GetCurrentUserId();
        if (!userId.HasValue)
        {
            _domainAccessScope = DomainAccessScope.Unscoped();
            _userActorScope = UserActorScope.Unrestricted();
            return;
        }

        var currentUserId = userId.Value;

        var scopedGroupMarkerRoleNames = await _identityDbContext.UserUserGroups
            .Where(x => x.UserId == currentUserId)
            .SelectMany(x => x.UserGroup.UserGroupRoles
                .Where(ugr =>
                    ugr.Role.Domain == nameof(StructurePermissions.KullaniciAtama)
                    && (ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir)
                        || ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir)
                        || ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir)
                        || ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir)
                        || ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir)
                        || ugr.Role.Name == nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir)))
                .Select(ugr => ugr.Role.Name))
            .Distinct()
            .ToListAsync(cancellationToken);

        var groupMarkerSet = scopedGroupMarkerRoleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isTesisManager = groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir))
            || groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir));
        var belongsToScopedGroup = isTesisManager
            || groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir))
            || groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir))
            || groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir))
            || groupMarkerSet.Contains(nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var managedTesisIds = await _stysDbContext.TesisYoneticileri
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var receptionistTesisIds = await _stysDbContext.TesisResepsiyonistleri
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var directBinaIds = await _stysDbContext.BinaYoneticileri
            .Where(x => x.UserId == currentUserId)
            .Select(x => x.BinaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var directTesisIds = managedTesisIds
            .Concat(receptionistTesisIds)
            .Distinct()
            .ToHashSet();

        var hasTesisLevelScope = directTesisIds.Count > 0;
        _domainAccessScope = await BuildDomainAccessScopeAsync(
            directTesisIds,
            directBinaIds,
            belongsToScopedGroup,
            hasTesisLevelScope,
            cancellationToken);

        if (!isTesisManager)
        {
            _userActorScope = UserActorScope.Unrestricted();
            return;
        }

        var managedBinaIds = await GetManagedBinaIdsForTesisManagerAsync(managedTesisIds, cancellationToken);
        var visibleUserIds = await GetVisibleUserIdsForTesisManagerAsync(
            managedTesisIds,
            cancellationToken);
        visibleUserIds.Add(currentUserId);

        _userActorScope = UserActorScope.TesisManagerScoped(managedTesisIds, managedBinaIds, visibleUserIds);
    }

    private async Task<DomainAccessScope> BuildDomainAccessScopeAsync(
        HashSet<int> directTesisIds,
        IReadOnlyCollection<int> directBinaIds,
        bool belongsToScopedGroup,
        bool hasTesisLevelScope,
        CancellationToken cancellationToken)
    {
        if (directTesisIds.Count == 0 && directBinaIds.Count == 0)
        {
            return belongsToScopedGroup
                ? DomainAccessScope.Scoped([], [], [])
                : DomainAccessScope.Unscoped();
        }

        var tesisIds = directTesisIds;
        var binaIds = directBinaIds.ToHashSet();

        if (binaIds.Count > 0)
        {
            var tesisIdsFromBina = await _stysDbContext.Binalar
                .Where(x => binaIds.Contains(x.Id))
                .Select(x => x.TesisId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var tesisId in tesisIdsFromBina)
            {
                tesisIds.Add(tesisId);
            }
        }

        if (hasTesisLevelScope && tesisIds.Count > 0)
        {
            var binaIdsFromTesis = await _stysDbContext.Binalar
                .Where(x => tesisIds.Contains(x.TesisId))
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var binaId in binaIdsFromTesis)
            {
                binaIds.Add(binaId);
            }
        }

        var ilIds = await _stysDbContext.Tesisler
            .Where(x => tesisIds.Contains(x.Id))
            .Select(x => x.IlId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return DomainAccessScope.Scoped(ilIds, tesisIds, binaIds);
    }

    private async Task<HashSet<Guid>> GetVisibleUserIdsForTesisManagerAsync(
        IReadOnlyCollection<int> managedTesisIds,
        CancellationToken cancellationToken)
    {
        if (managedTesisIds.Count == 0)
        {
            return [];
        }

        var managedTesisIdSet = managedTesisIds.ToHashSet();

        var tesisManagerUserIds = await _stysDbContext.TesisYoneticileri
            .Where(x => managedTesisIdSet.Contains(x.TesisId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var binaManagerUserIds = await (
            from binaYonetici in _stysDbContext.BinaYoneticileri
            join bina in _stysDbContext.Binalar on binaYonetici.BinaId equals bina.Id
            where managedTesisIdSet.Contains(bina.TesisId)
            select binaYonetici.UserId
        )
            .Distinct()
            .ToListAsync(cancellationToken);

        var receptionistUserIds = await _stysDbContext.TesisResepsiyonistleri
            .Where(x => managedTesisIdSet.Contains(x.TesisId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var ownerVisibleUserIds = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.TesisId.HasValue && managedTesisIdSet.Contains(x.TesisId.Value))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return tesisManagerUserIds
            .Concat(binaManagerUserIds)
            .Concat(receptionistUserIds)
            .Concat(ownerVisibleUserIds)
            .ToHashSet();
    }

    private async Task<HashSet<int>> GetManagedBinaIdsForTesisManagerAsync(
        IReadOnlyCollection<int> managedTesisIds,
        CancellationToken cancellationToken)
    {
        if (managedTesisIds.Count == 0)
        {
            return [];
        }

        var managedTesisIdSet = managedTesisIds.ToHashSet();
        return await _stysDbContext.Binalar
            .Where(x => managedTesisIdSet.Contains(x.TesisId))
            .Select(x => x.Id)
            .Distinct()
            .ToHashSetAsync(cancellationToken);
    }

    private bool IsCurrentUserAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return false;
        }

        var permissions = user
            .FindAll(TodPlatformAuthorizationConstants.PermissionClaimType)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x));

        return permissions.Any(permission =>
            permission.Equals(TodPlatformAuthorizationConstants.AdminPermission, StringComparison.OrdinalIgnoreCase)
            || permission.EndsWith(".Admin", StringComparison.OrdinalIgnoreCase));
    }
}
