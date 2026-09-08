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
    private readonly IMuhasebeDetayHesapService _muhasebeDetayHesapService;

    private sealed record SelectedHesapPlaniScope(int KurumId, int TesisId);

    public MuhasebeHesapPlaniService(
        IMuhasebeHesapPlaniRepository repository,
        IMapper mapper,
        IDistributedCache distributedCache,
        StysAppDbContext dbContext,
        IMuhasebeTesisScopeService tesisScopeService,
        ICurrentTenantAccessor currentTenantAccessor,
        IMuhasebeDetayHesapService muhasebeDetayHesapService)
        : base(repository, mapper)
    {
        _distributedCache = distributedCache;
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
        _currentTenantAccessor = currentTenantAccessor;
        _muhasebeDetayHesapService = muhasebeDetayHesapService;
    }

    public override async Task<MuhasebeHesapPlaniDto?> GetByIdAsync(
        int id,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        return await base.GetByIdAsync(id, BuildScopedIncludeQuery(null, include));
    }

    public async Task<MuhasebeHesapPlaniDto?> GetByIdAsync(int id, int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        var entity = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : Mapper.Map<MuhasebeHesapPlaniDto>(entity);
    }

    public override async Task<IEnumerable<MuhasebeHesapPlaniDto>> GetAllAsync(
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        return await base.GetAllAsync(BuildScopedIncludeQuery(null, include));
    }

    public override async Task<IEnumerable<MuhasebeHesapPlaniDto>> WhereAsync(
        System.Linq.Expressions.Expression<Func<MuhasebeHesapPlani, bool>> predicate,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null)
    {
        return await base.WhereAsync(predicate, BuildScopedIncludeQuery(null, include));
    }

    public override async Task<PagedResult<MuhasebeHesapPlaniDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<MuhasebeHesapPlani, bool>>? predicate = null,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include = null,
        Func<IQueryable<MuhasebeHesapPlani>, IOrderedQueryable<MuhasebeHesapPlani>>? orderBy = null)
    {
        return await base.GetPagedAsync(request, predicate, BuildScopedIncludeQuery(null, include), orderBy);
    }

    public async Task<PagedResult<MuhasebeHesapPlaniDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        return await base.GetPagedAsync(
            request,
            null,
            BuildScopedIncludeQuery(scope, null),
            q => q.OrderBy(x => x.TamKod).ThenBy(x => x.Id));
    }

    public override async Task<MuhasebeHesapPlaniDto> AddAsync(MuhasebeHesapPlaniDto dto)
    {
        await NormalizeAndValidateAsync(dto, null, null, CancellationToken.None);
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

        await NormalizeAndValidateAsync(dto, dto.Id.Value, null, CancellationToken.None);
        var updated = await base.UpdateAsync(dto);
        await InvalidateCacheAsync();
        return updated;
    }

    public async Task<MuhasebeHesapPlaniDto> UpdateAsync(MuhasebeHesapPlaniDto dto, int? tesisId, CancellationToken cancellationToken = default)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap plani id zorunludur.", 400);
        }

        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        await EnsureRecordInManageScopeAsync(dto.Id.Value, scope, cancellationToken);
        await NormalizeAndValidateAsync(dto, dto.Id.Value, scope, cancellationToken);
        var updated = await base.UpdateAsync(dto);
        await InvalidateCacheAsync();
        return updated;
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureRecordInManageScopeAsync(id, null, CancellationToken.None);
        await base.DeleteAsync(id);
        await InvalidateCacheAsync();
    }

    public async Task DeleteAsync(int id, int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        await EnsureRecordInManageScopeAsync(id, scope, cancellationToken);
        await base.DeleteAsync(id);
        await InvalidateCacheAsync();
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(CancellationToken cancellationToken = default)
        => await GetTreeCachedAsync(null, cancellationToken);

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        return await GetTreeCachedAsync(scope, cancellationToken);
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeRootsAsync(CancellationToken cancellationToken = default)
        => await GetTreeRootsAsync(null, cancellationToken);

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeRootsAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        var nodes = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .Where(x => x.SeviyeNo == 1)
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await MapTreeLevelAsync(nodes, scope, cancellationToken);
    }

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeChildrenAsync(int? parentId, CancellationToken cancellationToken = default)
        => await GetTreeChildrenAsync(parentId, null, cancellationToken);

    public async Task<List<MuhasebeHesapPlaniDto>> GetTreeChildrenAsync(int? parentId, int? tesisId, CancellationToken cancellationToken = default)
    {
        if (!parentId.HasValue)
        {
            return await GetTreeRootsAsync(tesisId, cancellationToken);
        }

        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);
        var parent = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .FirstOrDefaultAsync(x => x.Id == parentId.Value, cancellationToken);

        if (parent is null)
        {
            return [];
        }

        var prefix = $"{parent.TamKod}.";
        var childLevel = parent.SeviyeNo + 1;
        var nodes = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .Where(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix))
            .OrderBy(x => x.TamKod)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return await MapTreeLevelAsync(nodes, scope, cancellationToken);
    }

    public async Task<MuhasebeHesapPlaniDto> CreateDetayHesapAsync(
        int anaHesapId,
        string ad,
        int? tesisId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveSelectedScopeAsync(tesisId, cancellationToken);

        // 1) Ana hesap doğrulama: read scope'ta görünür, silinmemiş, aktif ve DETAY OLMAYAN parent.
        var anaHesap = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .FirstOrDefaultAsync(x => x.Id == anaHesapId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Ana hesap bulunamadı.", 404);

        if (!anaHesap.AktifMi)
        {
            throw new BaseException("Ana hesap pasif olduğu için altına detay hesap eklenemez.", 400);
        }

        if (anaHesap.DetayHesapMi)
        {
            throw new BaseException("Detay hesap altına yeni detay hesap eklenemez.", 400);
        }

        // 2) Ana hesap scope uyumu: global, seçili kurum veya seçili tesis olmalı.
        if (!IsParentScopeCompatible(scope, anaHesap))
        {
            throw new BaseException("Ana hesap ile çalışma tesisi kapsamı uyumlu değil.", 400);
        }

        var normalizedAd = (ad ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedAd))
        {
            throw new BaseException("Detay hesap adı zorunludur.", 400);
        }

        // 4) Idempotency: aynı tesis + üst hesap + aynı ad (trim) ile aktif detay hesap varsa onu döndür.
        var existing = await _dbContext.MuhasebeHesapPlanlari.AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted
                && x.AktifMi
                && x.KurumId == scope.KurumId
                && x.TesisId == scope.TesisId
                && x.UstHesapId == anaHesapId
                && x.DetayHesapMi
                && x.Ad == normalizedAd, cancellationToken);
        if (existing is not null)
        {
            return Mapper.Map<MuhasebeHesapPlaniDto>(existing);
        }

        // 5) Merkezi MuhasebeDetayHesapService ile güvenli kod/sayaç üretimi + oluşturma.
        var sonuc = await _muhasebeDetayHesapService.CreateOrResolveDetayHesapAsync(
            scope.TesisId,
            anaHesap.TamKod,
            "Manuel",
            normalizedAd,
            kaynakId: null,
            ustHesapId: anaHesap.Id,
            cancellationToken);

        await InvalidateCacheAsync();

        var created = await GetByIdAsync(sonuc.MuhasebeHesapPlaniId, scope.TesisId, cancellationToken);
        return created ?? throw new BaseException("Detay hesap oluşturulamadı.", 500);
    }

    private async Task<List<MuhasebeHesapPlaniDto>> MapTreeLevelAsync(
        List<MuhasebeHesapPlani> nodes,
        SelectedHesapPlaniScope? scope,
        CancellationToken cancellationToken)
    {
        var result = new List<MuhasebeHesapPlaniDto>(nodes.Count);
        foreach (var node in nodes)
        {
            var dto = Mapper.Map<MuhasebeHesapPlaniDto>(node);
            dto.HasChildren = await HasScopedChildrenAsync(node.TamKod, node.SeviyeNo, scope, cancellationToken);
            result.Add(dto);
        }

        return result;
    }

    private async Task<List<MuhasebeHesapPlaniDto>> GetTreeCachedAsync(SelectedHesapPlaniScope? scope, CancellationToken cancellationToken)
    {
        var version = await GetCacheVersionAsync(cancellationToken);
        var cacheKey = $"{TreeCacheKeyPrefix}:v{version}:{BuildScopeCacheSegment(scope)}";
        var payload = await _distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(payload))
        {
            var cached = JsonSerializer.Deserialize<List<MuhasebeHesapPlaniDto>>(payload);
            if (cached is not null)
            {
                return cached;
            }
        }

        var entities = await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
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

    private async Task NormalizeAndValidateAsync(
        MuhasebeHesapPlaniDto dto,
        int? currentId,
        SelectedHesapPlaniScope? manageScope,
        CancellationToken cancellationToken)
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

        if (dto.TesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(dto.TesisId.Value, cancellationToken);
            var tesisKurumId = await ResolveTesisKurumIdAsync(dto.TesisId.Value, cancellationToken);
            dto.KurumId ??= tesisKurumId;
            if (dto.KurumId != tesisKurumId)
            {
                throw new BaseException("Tesis ile kurum kapsamı uyumlu değil.", 400);
            }
        }
        else if (!dto.KurumId.HasValue)
        {
            if (!_currentTenantAccessor.IsSuperAdmin())
            {
                throw new BaseException("Global hesap planı kayıtları yalnızca SuperAdmin tarafından yönetilebilir.", 403);
            }
        }
        else
        {
            var currentKurumId = _currentTenantAccessor.GetCurrentKurumId();
            if (!_currentTenantAccessor.IsSuperAdmin() && currentKurumId != dto.KurumId)
            {
                throw new BaseException("Kurum kapsamı için yetkiniz bulunmuyor.", 403);
            }
        }

        MuhasebeHesapPlani? existing = null;
        if (currentId.HasValue)
        {
            existing = await ApplyManageScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), manageScope)
                .FirstOrDefaultAsync(x => x.Id == currentId.Value, cancellationToken);

            if (existing is null)
            {
                throw new BaseException("Hesap plani bulunamadi.", 404);
            }

            if (existing.KurumId != dto.KurumId || existing.TesisId != dto.TesisId)
            {
                throw new BaseException("Muhasebe kaydinin kurum/tesis kapsami degistirilemez.", 400);
            }
        }

        if (dto.UstHesapId.HasValue)
        {
            if (currentId.HasValue && dto.UstHesapId.Value == currentId.Value)
            {
                throw new BaseException("Bir hesap kendisinin ust hesabi olamaz.", 400);
            }

            var parent = await _dbContext.MuhasebeHesapPlanlari.AsNoTracking()
                .Where(x => !x.IsDeleted)
                .FirstOrDefaultAsync(x => x.Id == dto.UstHesapId.Value, cancellationToken);
            if (parent is null)
            {
                throw new BaseException("Secilen ust hesap bulunamadi.", 400);
            }

            if (!IsParentScopeCompatible(dto.KurumId, dto.TesisId, parent.KurumId, parent.TesisId))
            {
                throw new BaseException("Üst hesap ile tesis kapsamı uyumlu değil.", 400);
            }
        }

        var tamKodExists = await _dbContext.MuhasebeHesapPlanlari.AnyAsync(x =>
            x.TamKod == dto.TamKod
            && x.KurumId == dto.KurumId
            && x.TesisId == dto.TesisId
            && !x.IsDeleted
            && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken);
        if (tamKodExists)
        {
            throw new BaseException("Tam kod ayni tesis kapsami icinde benzersiz olmalidir.", 400);
        }

        var kodExists = await _dbContext.MuhasebeHesapPlanlari.AnyAsync(x =>
            x.Kod == dto.Kod
            && x.KurumId == dto.KurumId
            && x.TesisId == dto.TesisId
            && !x.IsDeleted
            && (!currentId.HasValue || x.Id != currentId.Value), cancellationToken);
        if (kodExists)
        {
            throw new BaseException("Kod ayni tesis kapsami icinde benzersiz olmalidir.", 400);
        }
    }

    private async Task<bool> HasScopedChildrenAsync(
        string parentTamKod,
        int parentLevel,
        SelectedHesapPlaniScope? scope,
        CancellationToken cancellationToken)
    {
        var prefix = $"{parentTamKod}.";
        var childLevel = parentLevel + 1;
        return await ApplyReadScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .AnyAsync(x => x.SeviyeNo == childLevel && x.TamKod.StartsWith(prefix), cancellationToken);
    }

    private IQueryable<MuhasebeHesapPlani> ApplyReadScope(IQueryable<MuhasebeHesapPlani> query, SelectedHesapPlaniScope? scope)
    {
        if (scope is null)
        {
            return query.Where(x => x.KurumId == null && x.TesisId == null);
        }

        return query.Where(x =>
            (x.KurumId == null && x.TesisId == null)
            || (x.KurumId == scope.KurumId && x.TesisId == null)
            || (x.KurumId == scope.KurumId && x.TesisId == scope.TesisId));
    }

    private IQueryable<MuhasebeHesapPlani> ApplyManageScope(IQueryable<MuhasebeHesapPlani> query, SelectedHesapPlaniScope? scope)
    {
        if (_currentTenantAccessor.IsSuperAdmin())
        {
            return scope is null ? query : ApplyReadScope(query, scope);
        }

        if (scope is null)
        {
            return query.Where(_ => false);
        }

        return query.Where(x =>
            (x.KurumId == scope.KurumId && x.TesisId == null)
            || (x.KurumId == scope.KurumId && x.TesisId == scope.TesisId));
    }

    private static bool IsParentScopeCompatible(int? childKurumId, int? childTesisId, int? parentKurumId, int? parentTesisId)
    {
        if (!parentKurumId.HasValue && !parentTesisId.HasValue)
        {
            return true;
        }

        if (!childKurumId.HasValue)
        {
            return false;
        }

        if (parentKurumId != childKurumId)
        {
            return false;
        }

        if (!parentTesisId.HasValue)
        {
            return true;
        }

        return childTesisId.HasValue && parentTesisId == childTesisId;
    }

    private static bool IsParentScopeCompatible(SelectedHesapPlaniScope scope, MuhasebeHesapPlani parent)
        => IsParentScopeCompatible(scope.KurumId, scope.TesisId, parent.KurumId, parent.TesisId);

    private async Task EnsureRecordInManageScopeAsync(int id, SelectedHesapPlaniScope? scope, CancellationToken cancellationToken)
    {
        var existing = await ApplyManageScope(_dbContext.MuhasebeHesapPlanlari.AsNoTracking(), scope)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (existing is null)
        {
            throw new BaseException("Hesap plani bulunamadi.", 404);
        }
    }

    private async Task<SelectedHesapPlaniScope> ResolveSelectedScopeAsync(int? tesisId, CancellationToken cancellationToken)
    {
        if (!tesisId.HasValue || tesisId.Value <= 0)
        {
            throw new BaseException("Çalışma tesisi seçilmelidir.", 400);
        }

        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
        var kurumId = await ResolveTesisKurumIdAsync(tesisId.Value, cancellationToken);
        return new SelectedHesapPlaniScope(kurumId, tesisId.Value);
    }

    private async Task<int> ResolveTesisKurumIdAsync(int tesisId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tesisler
            .AsNoTracking()
            .Where(x => x.Id == tesisId && x.AktifMi)
            .Select(x => (int?)x.KurumId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Seçilen tesis bulunamadı.", 400);
    }

    private static Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>> BuildScopedIncludeQuery(
        SelectedHesapPlaniScope? scope,
        Func<IQueryable<MuhasebeHesapPlani>, IQueryable<MuhasebeHesapPlani>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            return scope is null
                ? result.Where(x => x.KurumId == null && x.TesisId == null)
                : result.Where(x =>
                    (x.KurumId == null && x.TesisId == null)
                    || (x.KurumId == scope.KurumId && x.TesisId == null)
                    || (x.KurumId == scope.KurumId && x.TesisId == scope.TesisId));
        };
    }

    private static string BuildScopeCacheSegment(SelectedHesapPlaniScope? scope)
    {
        return scope is null
            ? "global-only"
            : $"kurum-{scope.KurumId}:tesis-{scope.TesisId}";
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
