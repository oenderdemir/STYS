using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Common.Services;

public class MuhasebeTesisScopeService : IMuhasebeTesisScopeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public MuhasebeTesisScopeService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        ICurrentTenantAccessor currentTenantAccessor)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public async Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        return await GetEffectiveTesisIdsAsync(scope, cancellationToken);
    }

    public async Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default)
    {
        var isSuperAdmin = _currentTenantAccessor.IsSuperAdmin();
        var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();

        if (!isSuperAdmin && !currentKurumId.HasValue)
        {
            return [];
        }

        var kurumId = currentKurumId.GetValueOrDefault();
        IQueryable<int> query;
        if (isSuperAdmin)
        {
            query = _dbContext.Tesisler
                .AsNoTracking()
                .Where(x => x.AktifMi)
                .Select(x => x.Id);
        }
        else
        {
            query = _dbContext.Tesisler
                .AsNoTracking()
                .Where(x => x.AktifMi && x.KurumId == kurumId)
                .Select(x => x.Id);
        }

        var activeTesisIds = await query.Distinct().ToListAsync(cancellationToken);

        if (!scope.IsScoped)
        {
            return activeTesisIds.ToArray();
        }

        var scopedIds = scope.TesisIds.ToHashSet();
        if (scopedIds.Count == 0)
        {
            return [];
        }

        return activeTesisIds.Where(scopedIds.Contains).Distinct().ToArray();
    }

    public async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        var tesis = await _dbContext.Tesisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tesisId && x.AktifMi, cancellationToken);

        if (tesis is null)
        {
            throw new BaseException("Seçilen tesis bulunamadı.", 404);
        }

        var effectiveTesisIds = await GetEffectiveTesisIdsAsync(cancellationToken);
        if (!effectiveTesisIds.Contains(tesisId))
        {
            throw new BaseException("Seçilen tesis için yetkiniz bulunmuyor.", 403);
        }
    }
}
