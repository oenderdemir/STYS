using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Binalar.Repositories;
using STYS.IsletmeAlanlari.Dto;
using STYS.IsletmeAlanlari.Entities;
using STYS.IsletmeAlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.IsletmeAlanlari.Services;

public class IsletmeAlaniService : BaseRdbmsService<IsletmeAlaniDto, IsletmeAlani, int>, IIsletmeAlaniService
{
    private readonly IIsletmeAlaniRepository _isletmeAlaniRepository;
    private readonly IIsletmeAlaniSinifiRepository _isletmeAlaniSinifiRepository;
    private readonly IBinaRepository _binaRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public IsletmeAlaniService(
        IIsletmeAlaniRepository isletmeAlaniRepository,
        IIsletmeAlaniSinifiRepository isletmeAlaniSinifiRepository,
        IBinaRepository binaRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(isletmeAlaniRepository, mapper)
    {
        _isletmeAlaniRepository = isletmeAlaniRepository;
        _isletmeAlaniSinifiRepository = isletmeAlaniSinifiRepository;
        _binaRepository = binaRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<IsletmeAlaniDto> AddAsync(IsletmeAlaniDto dto)
    {
        Normalize(dto);
        await EnsureBinaExistsAsync(dto.BinaId);
        await EnsureSinifExistsAsync(dto.IsletmeAlaniSinifiId);
        return await base.AddAsync(dto);
    }

    public override async Task<IsletmeAlaniDto> UpdateAsync(IsletmeAlaniDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Isletme alani id zorunludur.", 400);
        }

        var existingEntity = await _isletmeAlaniRepository.GetByIdAsync(dto.Id.Value);
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek isletme alani bulunamadi.", 404);
        }

        await EnsureCanAccessBinaAsync(existingEntity.BinaId);
        Normalize(dto);
        await EnsureBinaExistsAsync(dto.BinaId);
        await EnsureSinifExistsAsync(dto.IsletmeAlaniSinifiId);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<IsletmeAlaniSinifiDto>> GetSiniflarAsync(bool onlyActive, CancellationToken cancellationToken = default)
    {
        var query = _isletmeAlaniSinifiRepository.Where(x => !onlyActive || x.AktifMi);

        var entities = await query
            .OrderBy(x => x.Ad)
            .ToListAsync(cancellationToken);
        return entities.Select(entity => Mapper.Map<IsletmeAlaniSinifiDto>(entity)).ToList();
    }

    public async Task<PagedResult<IsletmeAlaniSinifiDto>> GetSiniflarPagedAsync(
        PagedRequest request,
        string? query,
        Func<IQueryable<IsletmeAlaniSinifi>, IOrderedQueryable<IsletmeAlaniSinifi>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();
        var predicate = string.IsNullOrWhiteSpace(normalizedQuery)
            ? null
            : (Expression<Func<IsletmeAlaniSinifi, bool>>)(x => x.Ad.Contains(normalizedQuery) || x.Kod.Contains(normalizedQuery));

        var pagedEntities = await _isletmeAlaniSinifiRepository.GetPagedAsync(
            request,
            predicate: predicate,
            orderBy: orderBy ?? (q => q.OrderBy(x => x.Ad).ThenBy(x => x.Id)));

        return new PagedResult<IsletmeAlaniSinifiDto>(
            pagedEntities.Items.Select(entity => Mapper.Map<IsletmeAlaniSinifiDto>(entity)).ToList(),
            pagedEntities.PageNumber,
            pagedEntities.PageSize,
            pagedEntities.TotalCount);
    }

    public async Task<IsletmeAlaniSinifiDto?> GetSinifByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _isletmeAlaniSinifiRepository.GetByIdAsync(id);
        return entity is null ? null : Mapper.Map<IsletmeAlaniSinifiDto>(entity);
    }

    public async Task<IsletmeAlaniSinifiDto> AddSinifAsync(IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default)
    {
        NormalizeSinif(dto);
        await EnsureUniqueSinifAsync(dto, null);

        var entity = Mapper.Map<IsletmeAlaniSinifi>(dto);
        await _isletmeAlaniSinifiRepository.AddAsync(entity);
        await _isletmeAlaniSinifiRepository.SaveChangesAsync(cancellationToken);
        return Mapper.Map<IsletmeAlaniSinifiDto>(entity);
    }

    public async Task<IsletmeAlaniSinifiDto> UpdateSinifAsync(IsletmeAlaniSinifiDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Isletme alani sinifi id zorunludur.", 400);
        }

        var entity = await _isletmeAlaniSinifiRepository.GetByIdAsync(dto.Id.Value);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek isletme alani sinifi bulunamadi.", 404);
        }

        NormalizeSinif(dto);
        await EnsureUniqueSinifAsync(dto, dto.Id.Value);

        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.AktifMi = dto.AktifMi;
        entity.IsDeleted = false;

        _isletmeAlaniSinifiRepository.Update(entity);
        await _isletmeAlaniSinifiRepository.SaveChangesAsync(cancellationToken);
        return Mapper.Map<IsletmeAlaniSinifiDto>(entity);
    }

    public async Task DeleteSinifAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _isletmeAlaniSinifiRepository.GetByIdAsync(id);
        if (entity is null)
        {
            throw new BaseException("Silinecek isletme alani sinifi bulunamadi.", 404);
        }

        var inUse = await _isletmeAlaniRepository.AnyAsync(x => x.IsletmeAlaniSinifiId == id);
        if (inUse)
        {
            throw new BaseException("Bu sinif kullanimda oldugu icin silinemez.", 400);
        }

        _isletmeAlaniSinifiRepository.Delete(entity);
        await _isletmeAlaniSinifiRepository.SaveChangesAsync(cancellationToken);
    }

    public override async Task DeleteAsync(int id)
    {
        var existingEntity = await _isletmeAlaniRepository.GetByIdAsync(id);
        if (existingEntity is null)
        {
            throw new BaseException("Silinecek isletme alani bulunamadi.", 404);
        }

        await EnsureCanAccessBinaAsync(existingEntity.BinaId);
        await base.DeleteAsync(id);
    }

    public override async Task<IsletmeAlaniDto?> GetByIdAsync(int id, Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<IsletmeAlaniDto>> GetAllAsync(Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<IsletmeAlaniDto>> WhereAsync(Expression<Func<IsletmeAlani, bool>> predicate, Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<IsletmeAlaniDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<IsletmeAlani, bool>>? predicate = null,
        Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>>? include = null,
        Func<IQueryable<IsletmeAlani>, IOrderedQueryable<IsletmeAlani>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureBinaExistsAsync(int binaId)
    {
        var bina = await _binaRepository.GetByIdAsync(binaId);
        if (bina is null)
        {
            throw new BaseException("Secilen bina bulunamadi.", 400);
        }

        await EnsureCanAccessBinaAsync(binaId);
    }

    private async Task EnsureSinifExistsAsync(int sinifId)
    {
        var sinif = await _isletmeAlaniSinifiRepository.GetByIdAsync(sinifId);
        if (sinif is null || !sinif.AktifMi)
        {
            throw new BaseException("Secilen isletme alani sinifi bulunamadi veya pasif.", 400);
        }
    }

    private static void NormalizeSinif(IsletmeAlaniSinifiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Sinif kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Sinif adi zorunludur.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
    }

    private async Task EnsureUniqueSinifAsync(IsletmeAlaniSinifiDto dto, int? excludedId)
    {
        var normalizedKod = dto.Kod.Trim().ToUpperInvariant();
        var normalizedAd = dto.Ad.Trim().ToUpperInvariant();

        var exists = await _isletmeAlaniSinifiRepository.AnyAsync(x =>
            (x.Kod.ToUpper() == normalizedKod || x.Ad.ToUpper() == normalizedAd)
            && (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni kod veya adda isletme alani sinifi zaten mevcut.", 400);
        }
    }

    private static void Normalize(IsletmeAlaniDto dto)
    {
        if (dto.BinaId <= 0)
        {
            throw new BaseException("Bina secimi zorunludur.", 400);
        }

        if (dto.IsletmeAlaniSinifiId <= 0)
        {
            throw new BaseException("Isletme alani sinifi secimi zorunludur.", 400);
        }

        dto.OzelAd = string.IsNullOrWhiteSpace(dto.OzelAd) ? null : dto.OzelAd.Trim();
        dto.Ad = dto.OzelAd ?? string.Empty;
    }

    private async Task EnsureCanAccessBinaAsync(int binaId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.BinaIds.Contains(binaId))
        {
            throw new BaseException("Bu bina altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<IsletmeAlani>, IQueryable<IsletmeAlani>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            result = result.Include(x => x.IsletmeAlaniSinifi);
            if (scope.IsScoped)
            {
                result = result.Where(x => scope.BinaIds.Contains(x.BinaId));
            }

            return result;
        };
    }
}
