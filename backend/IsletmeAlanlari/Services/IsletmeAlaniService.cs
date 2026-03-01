using AutoMapper;
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
    private readonly IBinaRepository _binaRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public IsletmeAlaniService(
        IIsletmeAlaniRepository isletmeAlaniRepository,
        IBinaRepository binaRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(isletmeAlaniRepository, mapper)
    {
        _isletmeAlaniRepository = isletmeAlaniRepository;
        _binaRepository = binaRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<IsletmeAlaniDto> AddAsync(IsletmeAlaniDto dto)
    {
        Normalize(dto);
        await EnsureBinaExistsAsync(dto.BinaId);
        await EnsureUniqueActiveNameAsync(dto, null);
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
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
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

    private async Task EnsureUniqueActiveNameAsync(IsletmeAlaniDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _isletmeAlaniRepository.AnyAsync(x =>
            x.AktifMi &&
            x.BinaId == dto.BinaId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni bina altinda ayni isimde aktif isletme alani zaten mevcut.", 400);
        }
    }

    private static void Normalize(IsletmeAlaniDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Isletme alani adi zorunludur.", 400);
        }

        if (dto.BinaId <= 0)
        {
            throw new BaseException("Bina secimi zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
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
            if (scope.IsScoped)
            {
                result = result.Where(x => scope.BinaIds.Contains(x.BinaId));
            }

            return result;
        };
    }
}
