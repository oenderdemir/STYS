using AutoMapper;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.SezonKurallari.Dto;
using STYS.SezonKurallari.Entities;
using STYS.SezonKurallari.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.SezonKurallari.Services;

public class SezonKuraliService : BaseRdbmsService<SezonKuraliDto, SezonKurali, int>, ISezonKuraliService
{
    private readonly ISezonKuraliRepository _sezonKuraliRepository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public SezonKuraliService(
        ISezonKuraliRepository sezonKuraliRepository,
        ITesisRepository tesisRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(sezonKuraliRepository, mapper)
    {
        _sezonKuraliRepository = sezonKuraliRepository;
        _tesisRepository = tesisRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<SezonKuraliDto> AddAsync(SezonKuraliDto dto)
    {
        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueCodeAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<SezonKuraliDto> UpdateAsync(SezonKuraliDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Sezon kurali id zorunludur.", 400);
        }

        var existing = await _sezonKuraliRepository.GetByIdAsync(dto.Id.Value);
        if (existing is null)
        {
            throw new BaseException("Guncellenecek sezon kurali bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existing.TesisId);
        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueCodeAsync(dto, dto.Id.Value);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var existing = await _sezonKuraliRepository.GetByIdAsync(id);
        if (existing is null)
        {
            throw new BaseException("Silinecek sezon kurali bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existing.TesisId);
        await base.DeleteAsync(id);
    }

    public override async Task<SezonKuraliDto?> GetByIdAsync(int id, Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var scopedInclude = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, scopedInclude);
    }

    public override async Task<IEnumerable<SezonKuraliDto>> GetAllAsync(Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var scopedInclude = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(scopedInclude);
    }

    public override async Task<IEnumerable<SezonKuraliDto>> WhereAsync(Expression<Func<SezonKurali, bool>> predicate, Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var scopedInclude = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, scopedInclude);
    }

    public override async Task<PagedResult<SezonKuraliDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<SezonKurali, bool>>? predicate = null,
        Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>>? include = null,
        Func<IQueryable<SezonKurali>, IOrderedQueryable<SezonKurali>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var scopedInclude = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, scopedInclude, orderBy);
    }

    private async Task EnsureTesisRulesAsync(int tesisId)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        var tesis = await _tesisRepository.GetByIdAsync(tesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 400);
        }

        if (!tesis.AktifMi)
        {
            throw new BaseException("Pasif tesis icin sezon kurali tanimlanamaz.", 400);
        }

        await EnsureCanAccessTesisAsync(tesisId);
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis icin sezon kurali yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureUniqueCodeAsync(SezonKuraliDto dto, int? excludedId)
    {
        var normalizedCode = dto.Kod.Trim().ToUpperInvariant();
        var exists = await _sezonKuraliRepository.AnyAsync(x =>
            x.TesisId == dto.TesisId
            && x.Kod.ToUpper() == normalizedCode
            && (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni kodla baska bir sezon kurali mevcut.", 400);
        }
    }

    private static void Normalize(SezonKuraliDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kod zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Ad zorunludur.", 400);
        }

        if (dto.BaslangicTarihi.Date > dto.BitisTarihi.Date)
        {
            throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
        }

        if (dto.MinimumGece <= 0)
        {
            throw new BaseException("Minimum gece en az 1 olmalidir.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.BaslangicTarihi = dto.BaslangicTarihi.Date;
        dto.BitisTarihi = dto.BitisTarihi.Date;
    }

    private static Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<SezonKurali>, IQueryable<SezonKurali>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => scope.TesisIds.Contains(x.TesisId));
            }

            return result;
        };
    }
}
