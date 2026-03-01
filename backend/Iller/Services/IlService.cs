using AutoMapper;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Iller.Dto;
using STYS.Iller.Entities;
using STYS.Iller.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Iller.Services;

public class IlService : BaseRdbmsService<IlDto, Il, int>, IIlService
{
    private readonly IIlRepository _ilRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public IlService(IIlRepository ilRepository, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(ilRepository, mapper)
    {
        _ilRepository = ilRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<IlDto> AddAsync(IlDto dto)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            throw new BaseException("Il olusturma islemi kapsamli kullanicilar icin kapatilmis durumdadir.", 403);
        }

        Normalize(dto);
        await EnsureUniqueActiveNameAsync(dto.Ad, dto.AktifMi);
        return await base.AddAsync(dto);
    }

    public override async Task<IlDto> UpdateAsync(IlDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Il id zorunludur.", 400);
        }

        await EnsureCanAccessIlAsync(dto.Id.Value);
        Normalize(dto);
        await EnsureUniqueActiveNameAsync(dto.Ad, dto.AktifMi, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanAccessIlAsync(id);
        await base.DeleteAsync(id);
    }

    public override async Task<IlDto?> GetByIdAsync(int id, Func<IQueryable<Il>, IQueryable<Il>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<IlDto>> GetAllAsync(Func<IQueryable<Il>, IQueryable<Il>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<IlDto>> WhereAsync(Expression<Func<Il, bool>> predicate, Func<IQueryable<Il>, IQueryable<Il>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<IlDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<Il, bool>>? predicate = null,
        Func<IQueryable<Il>, IQueryable<Il>>? include = null,
        Func<IQueryable<Il>, IOrderedQueryable<Il>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureUniqueActiveNameAsync(string name, bool isActive, int? excludedId = null)
    {
        if (!isActive)
        {
            return;
        }

        var normalizedName = name.Trim().ToUpperInvariant();
        var exists = await _ilRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni isimde aktif il zaten mevcut.", 400);
        }
    }

    private static void Normalize(IlDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Il adi zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }

    private async Task EnsureCanAccessIlAsync(int ilId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.IlIds.Contains(ilId))
        {
            throw new BaseException("Bu il kaydi icin yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<Il>, IQueryable<Il>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Il>, IQueryable<Il>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => scope.IlIds.Contains(x.Id));
            }

            return result;
        };
    }
}
