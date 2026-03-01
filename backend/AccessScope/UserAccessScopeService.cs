using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Security.Auth.Services;

namespace STYS.AccessScope;

public class UserAccessScopeService : IUserAccessScopeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private DomainAccessScope? _cachedScope;

    public UserAccessScopeService(
        StysAppDbContext dbContext,
        ICurrentUserAccessor currentUserAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedScope is not null)
        {
            return _cachedScope;
        }

        if (IsCurrentUserAdmin())
        {
            _cachedScope = DomainAccessScope.Unscoped();
            return _cachedScope;
        }

        var userId = _currentUserAccessor.GetCurrentUserId();
        if (!userId.HasValue)
        {
            _cachedScope = DomainAccessScope.Unscoped();
            return _cachedScope;
        }

        var directTesisIds = await _dbContext.TesisYoneticileri
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var directBinaIds = await _dbContext.BinaYoneticileri
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.BinaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (directTesisIds.Count == 0 && directBinaIds.Count == 0)
        {
            _cachedScope = DomainAccessScope.Unscoped();
            return _cachedScope;
        }

        var tesisIds = directTesisIds.ToHashSet();
        var binaIds = directBinaIds.ToHashSet();

        if (binaIds.Count > 0)
        {
            var tesisIdsFromBina = await _dbContext.Binalar
                .Where(x => binaIds.Contains(x.Id))
                .Select(x => x.TesisId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var tesisId in tesisIdsFromBina)
            {
                tesisIds.Add(tesisId);
            }
        }

        if (tesisIds.Count > 0)
        {
            var binaIdsFromTesis = await _dbContext.Binalar
                .Where(x => tesisIds.Contains(x.TesisId))
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var binaId in binaIdsFromTesis)
            {
                binaIds.Add(binaId);
            }
        }

        var ilIds = await _dbContext.Tesisler
            .Where(x => tesisIds.Contains(x.Id))
            .Select(x => x.IlId)
            .Distinct()
            .ToListAsync(cancellationToken);

        _cachedScope = DomainAccessScope.Scoped(ilIds, tesisIds, binaIds);
        return _cachedScope;
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
