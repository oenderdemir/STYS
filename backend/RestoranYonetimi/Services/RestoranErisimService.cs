using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Restoranlar.Entities;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.RestoranYonetimi.Services;

public class RestoranErisimService : IRestoranErisimService
{
    private readonly StysAppDbContext _dbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private bool _initialized;
    private bool _unrestricted = true;
    private HashSet<int> _yetkiliRestoranIdleri = [];

    public RestoranErisimService(
        StysAppDbContext dbContext,
        ICurrentUserAccessor currentUserAccessor,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUserAccessor = currentUserAccessor;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyCollection<int>?> GetYetkiliRestoranIdleriAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return _unrestricted ? null : _yetkiliRestoranIdleri;
    }

    public async Task<IQueryable<Restoran>> ApplyRestoranScopeAsync(IQueryable<Restoran> query, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_unrestricted)
        {
            return query;
        }

        return query.Where(x => _yetkiliRestoranIdleri.Contains(x.Id));
    }

    public async Task EnsureRestoranErisimiAsync(int restoranId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (_unrestricted)
        {
            return;
        }

        if (!_yetkiliRestoranIdleri.Contains(restoranId))
        {
            throw new BaseException("Bu restoran icin yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var userId = _currentUserAccessor.GetCurrentUserId();
        if (!userId.HasValue)
        {
            _unrestricted = true;
            return;
        }

        var userPermissions = GetCurrentUserPermissionSet();
        var isAdmin = userPermissions.Contains(TodPlatformAuthorizationConstants.AdminPermission)
            || userPermissions.Any(x => x.EndsWith(".Admin", StringComparison.OrdinalIgnoreCase));
        var shouldBeScoped = userPermissions.Contains(StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir)
            || userPermissions.Contains(StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir)
            || userPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir)
            || userPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtayabilir)
            || userPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir)
            || userPermissions.Contains(StructurePermissions.KullaniciAtama.RestoranGarsonuAtayabilir);
        var assignedRestoranIds = await _dbContext.RestoranYoneticileri
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.RestoranId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var assignedRestoranIdsAsGarson = await _dbContext.RestoranGarsonlari
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.RestoranId)
            .Distinct()
            .ToListAsync(cancellationToken);

        assignedRestoranIds = assignedRestoranIds
            .Concat(assignedRestoranIdsAsGarson)
            .Distinct()
            .ToList();

        if (isAdmin)
        {
            _unrestricted = true;
            return;
        }

        var managedTesisIds = await _dbContext.TesisYoneticileri
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var scopeRestoranIds = assignedRestoranIds;
        if (managedTesisIds.Count > 0)
        {
            var tesisBazliRestoranIds = await _dbContext.Restoranlar
                .Where(x => managedTesisIds.Contains(x.TesisId))
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            scopeRestoranIds = scopeRestoranIds
                .Concat(tesisBazliRestoranIds)
                .Distinct()
                .ToList();
        }

        if (scopeRestoranIds.Count == 0)
        {
            // Tesis yoneticisi/restoran yoneticisi/garson rolleri yalnizca kendi kapsamlarini gorebilir.
            _unrestricted = !shouldBeScoped;
            _yetkiliRestoranIdleri = [];
            return;
        }

        _unrestricted = false;
        _yetkiliRestoranIdleri = scopeRestoranIds.ToHashSet();
    }

    private HashSet<string> GetCurrentUserPermissionSet()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return user
            .FindAll(TodPlatformAuthorizationConstants.PermissionClaimType)
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
