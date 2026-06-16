using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Dtos;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using System.Text.Json;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Services;

public class MuhasebeHesapPlaniService
    : BaseRdbmsService<MuhasebeHesapPlaniDto, MuhasebeHesapPlani, int>,
      IMuhasebeHesapPlaniService
{
    private const string CacheVersionKey = "Muhasebe:HesapPlani:CacheVersion";
    private const string TreeCacheKeyPrefix = "Muhasebe:HesapPlani:Tree";

    private readonly IDistributedCache _distributedCache;
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public MuhasebeHesapPlaniService(
        IMuhasebeHesapPlaniRepository repository,
        IMapper mapper,
        IDistributedCache distributedCache,
        StysAppDbContext dbContext,
        IMuhasebeTesisScopeService tesisScopeService,
        ICurrentTenantAccessor currentTenantAccessor)
        : base(repository, mapper)
    {
        _distributedCache = distributedCache;
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public override async Task<MuhasebeHesapPlaniDto?> GetByIdAsync(
        int id,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync();
        return await base.GetByIdAsync(id, BuildScopedIncludeQuery(effectiveTesisIds, include));
    }

    public override async Task<IEnumerable<MuhasebeHesapPlaniDto>> GetAllAsync(
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync();
        return await base.GetAllAsync(BuildScopedIncludeQuery(effectiveTesisIds, include));
    }

    public override async Task<IEnumerable<MuhasebeHesapPlaniDto>> WhereAsync(
        System.Linq.Expressions.Expression<Func<MuhasebeHesapPlani, bool>> predicate,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync();
        return await base.WhereAsync(predicate, BuildScopedIncludeQuery(effectiveTesisIds, include));
    }

    public override async Task<PagedResult<MuhasebeHesapPlaniDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<MuhasebeHesapPlani, bool>>? predicate = null,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null,
        Func<IQueryable<MuhasebeHesapPlani>, IOrderedQueryable<MuhasebeHesapPlani>>? orderBy = null)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync();
        return await base.GetPagedAsync(request, predicate, BuildScopedIncludeQuery(effectiveTesisIds, include), orderBy);
    }

    public override async Task<MuhasebeHesapPlaniDto> AddAsync(MuhasebeHesapPlaniDto dto)
    {
        await NormalizeAndValidateAsync(dto, null, CancellationToken.None);
        var created = await base.AddAsync(dto);
        await InvalidateCacheAsync();
        return created;
    }

    public override async Task<MuhasebeHesapPlaniDto> UpdateAsync(MuhasebeHesapPlaniDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap plani id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id.Value, CancellationToken.None);
        var updated = await base.UpdateAsync(dto);
        await InvalidateCacheAsync();
        return updated;
    }

    public override async Task DeleteAsync(int id)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync();
        var existing = await ApplyManageScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existing is null)
        {
            throw new BaseException("Hesap plani bulunamadi.", 404);
        }

        await base.DeleteAsync(id);
        await InvalidateCacheAsync();
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default)
        => await GetTreeCachedAsync(cancellationToken);

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeRootsAsync(CancellationToken cancellationToken = default)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        var nodes = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .Where(x => x.SeviyeNo == 1)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await MapTreeLevelAsync(nodes, effectiveTesisIds, cancellationToken);
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeChildrenAsync(int? parentId, CancellationToken cancellationToken = default)
    {
        if (!parentId.HasValue)
        {
            return await GetTreeRootsAsync(cancellationToken);
        }

        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        var parent = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .FirstOrDefaultAsync(x => x.Id == parentId.Value, cancellationToken);

        if (parent is null)
        {
            return [];
        }

        var prefix = $"{parent.TamKod}.";
        var childLevel = parent.SeviyeNo + 1;
        var nodes = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .Where(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix))
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await MapTreeLevelAsync(nodes, effectiveTesisIds, cancellationToken);
    }

    private async Task<List<MuhasebeHesapPlaniDto>> MapTreeLevelAsync(
        List<MuhasebeHesapPlani> nodes,
        int[] effectiveTesisIds,
        CancellationToken cancellationToken)
    {
        var result = new List<MuhasebeHesapPlaniDto>(nodes.Count);
        foreach (var node in nodes)
        {
            var dto = Mapper.Map<MuhasebeHesapPlaniDto>(node);
            dto.HasChildren = await HasScopedChildrenAsync(node.TamKod, node.SeviyeNo, effectiveTesisIds, cancellationToken);
            result.Add(dto);
        }

        return result;
    }

    private async Task<List<MuhasebeHesapPlaniDto>> GetTreeCachedAsync(CancellationToken cancellationToken)
    {
        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        var version = await GetCacheVersionAsync(cancellationToken);
        var cacheKey = $"{TreeCacheKeyPrefix}:v{version}:{BuildScopeCacheSegment(_currentTenantAccessor.IsSuperAdmin(), effectiveTesisIds)}";
        var payload = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload))
        {
            var cached = JsonSerializer.Deserialize<List<MuhasebeHesapPlaniDto>>(payload);
            if (cached is not null)
            {
                return cached;
            }
        }

        var entities = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var items = entities
            .Select(x => Mapper.Map<MuhasebeHesapPlaniDto>(x))
            .ToList();

        var serialized = JsonSerializer.Serialize(items);
        await _distributedCache.SetStringAsync(cacheKey, serialized, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        }, cancellationToken);

        return items;
    }

    private async Task NormalizeAndValidateAsync(MuhasebeHesapPlaniDto dto, int? currentId, CancellationToken cancellationToken)
    {
        dto.Kod = (dto.Kod ?? string.Empty).Trim();
        dto.TamKod = (dto.TamKod ?? string.Empty).Trim();
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.TamKod))
        {
            throw new BaseException("Tam kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (dto.SeviyeNo <= 0)
        {
            throw new BaseException("Seviye no 0'dan buyuk olmalidir.", 400);
        }

        if (dto.HareketGorebilirMi && !dto.DetayHesapMi)
        {
            throw new BaseException("Hareket gorebilir hesap ayni zamanda detay hesap olmalidir.", 400);
        }

        var effectiveTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        if (dto.TesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(dto.TesisId.Value, cancellationToken);
        }
        else if (!_currentTenantAccessor.IsSuperAdmin())
        {
            throw new BaseException("Global hesap planı kayıtları yalnızca SuperAdmin tarafından yönetilebilir.", 403);
        }

        if (dto.TesisId.HasValue)
        {
            var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == dto.TesisId.Value && x.AktifMi, cancellationToken);
            if (!tesisExists)
            {
                throw new BaseException("Seçilen tesis bulunamadı.", 400);
            }
        }

        MuhasebeHesapPlani? existing = null;
        if (currentId.HasValue)
        {
            existing = await ApplyManageScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
                .FirstOrDefaultAsync(x => x.Id == currentId.Value, cancellationToken);

            if (existing is null)
            {
                throw new BaseException("Hesap plani bulunamadi.", 404);
            }

            if (existing.TesisId != dto.TesisId)
            {
                throw new BaseException("Muhasebe kaydinin tesisi degistirilemez.", 400);
            }
        }

        if (dto.UstHesapId.HasValue)
        {
            if (currentId.HasValue && dto.UstHesapId.Value == currentId.Value)
            {
                throw new BaseException("Bir hesap kendisinin ust hesabi olamaz.", 400);
            }

            var parent = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
                .FirstOrDefaultAsync(x => x.Id == dto.UstHesapId.Value, cancellationToken);
            if (parent is null)
            {
                throw new BaseException("Secilen ust hesap bulunamadi.", 400);
            }

            if (!IsParentScopeCompatible(dto.TesisId, parent.TesisId))
            {
                throw new BaseException("Üst hesap ile tesis kapsamı uyumlu değil.", 400);
            }
        }

        var tamKodExists = await _dbContext.MuhasebeHesapPlanlari.AnyAsync(x =>
            x.TamKod == dto.TamKod
            && x.TesisId == dto.TesisId
            && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken);
        if (tamKodExists)
        {
            throw new BaseException("Tam kod ayni tesis kapsami icinde benzersiz olmalidir.", 400);
        }

        var kodExists = await _dbContext.MuhasebeHesapPlanlari.AnyAsync(x =>
            x.Kod == dto.Kod
            && x.TesisId == dto.TesisId
            && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken);
        if (kodExists)
        {
            throw new BaseException("Kod ayni tesis kapsami icinde benzersiz olmalidir.", 400);
        }
    }

    private async Task<bool> HasScopedChildrenAsync(
        string parentTamKod,
        int parentLevel,
        int[] effectiveTesisIds,
        CancellationToken cancellationToken)
    {
        var prefix = $"{parentTamKod}.";
        var childLevel = parentLevel + 1;
        return await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), effectiveTesisIds)
            .AnyAsync(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix), cancellationToken);
    }

    private IQueryable<MuhasebeHesapPlani> ApplyReadScope(IQueryable<MuhasebeHesapPlani> query, int[] effectiveTesisIds)
    {
        if (effectiveTesisIds.Length == 0)
        {
            return query.Where(x => x.TesisId == null);
        }

        return query.Where(x =>
            x.TesisId == null
            || (x.TesisId.HasValue && effectiveTesisIds.Contains(x.TesisId.Value)));
    }

    private IQueryable<MuhasebeHesapPlani> ApplyManageScope(IQueryable<MuhasebeHesapPlani> query, int[] effectiveTesisIds)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return query;
        }

        if (effectiveTesisIds.Length == 0)
        {
            return query.Where(_ => false);
        }

        return query.Where(x => x.TesisId.HasValue && effectiveTesisIds.Contains(x.TesisId.Value));
    }

    private static bool IsParentScopeCompatible(int? childTesisId, int? parentTesisId)
    {
        if (!childTesisId.HasValue)
        {
            return !parentTesisId.HasValue;
        }

        return !parentTesisId.HasValue || parentTesisId == childTesisId;
    }

    private static Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>> BuildScopedIncludeQuery(
        int[] effectiveTesisIds,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (effectiveTesisIds.Length == 0)
            {
                return result.Where(x => x.TesisId == null);
            }

            return result.Where(x =>
                x.TesisId == null
                || (x.TesisId.HasValue && effectiveTesisIds.Contains(x.TesisId.Value)));
        };
    }

    private static string BuildScopeCacheSegment(bool isSuperAdmin, int[] effectiveTesisIds)
    {
        if (isSuperAdmin)
        {
            return "all";
        }

        return effectiveTesisIds.Length == 0
            ? "global-only"
            : $"tesis-{string.Join('-', effectiveTesisIds.OrderBy(x => x))}";
    }

    private async Task<string> GetCacheVersionAsync(CancellationToken cancellationToken)
    {
        var version = await _distributedCache.GetStringAsync(CacheVersionKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        const string initialVersion = "1";
        await _distributedCache.SetStringAsync(CacheVersionKey, initialVersion, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
        }, cancellationToken);
        return initialVersion;
    }

    private async Task InvalidateCacheAsync()
    {
        await _distributedCache.SetStringAsync(CacheVersionKey, Guid.NewGuid().ToString("N"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
        });
    }
}
