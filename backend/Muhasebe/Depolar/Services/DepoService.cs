using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.Depolar.Dtos;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Tesisler.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Depolar.Services;

public class DepoService : BaseRdbmsService<DepoDto, Depo, int>, IDepoService
{
    private readonly IDepoRepository _repository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public DepoService(IDepoRepository repository, ITesisRepository tesisRepository, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _tesisRepository = tesisRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<DepoDto> AddAsync(DepoDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        await NormalizeAndValidateAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<DepoDto> UpdateAsync(DepoDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Depo id zorunludur.", 400);
        }

        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id);
        await NormalizeAndValidateAsync(dto, dto.Id);
        return await base.UpdateAsync(dto);
    }

    public override async Task<DepoDto?> GetByIdAsync(int id, Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<DepoDto>> GetAllAsync(Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<DepoDto>> WhereAsync(System.Linq.Expressions.Expression<Func<Depo, bool>> predicate, Func<IQueryable<Depo>, IQueryable<Depo>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<DepoDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<Depo, bool>>? predicate = null,
        Func<IQueryable<Depo>, IQueryable<Depo>>? include = null,
        Func<IQueryable<Depo>, IOrderedQueryable<Depo>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task NormalizeAndValidateAsync(DepoDto dto, int? currentId)
    {
        dto.Kod = dto.Kod?.Trim().ToUpperInvariant() ?? string.Empty;
        dto.Ad = dto.Ad?.Trim() ?? string.Empty;
        dto.Aciklama = string.IsNullOrWhiteSpace(dto.Aciklama) ? null : dto.Aciklama.Trim();

        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Depo kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Depo adi zorunludur.", 400);
        }

        if (dto.TesisId.HasValue && dto.TesisId.Value > 0)
        {
            var tesisExists = await _tesisRepository.AnyAsync(x => x.Id == dto.TesisId.Value);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        var duplicate = await _repository.AnyAsync(x => x.Kod == dto.Kod && (!currentId.HasValue || x.Id != currentId.Value));
        if (duplicate)
        {
            throw new BaseException("Depo kodu benzersiz olmalidir.", 400);
        }
    }

    private async Task<int?> ResolveWriteTesisIdAsync(int? tesisId, int? existingId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var candidateTesisId = tesisId;

        if (!candidateTesisId.HasValue && existingId.HasValue)
        {
            candidateTesisId = await _repository.Where(x => x.Id == existingId.Value).Select(x => x.TesisId).FirstOrDefaultAsync();
        }

        if (scope.IsScoped)
        {
            if (!candidateTesisId.HasValue)
            {
                if (scope.TesisIds.Count == 1)
                {
                    candidateTesisId = scope.TesisIds.First();
                }
                else
                {
                    throw new BaseException("Tesis secimi zorunludur.", 400);
                }
            }

            if (!scope.TesisIds.Contains(candidateTesisId.Value))
            {
                throw new BaseException("Secilen tesis icin yetkiniz bulunmuyor.", 403);
            }
        }

        return candidateTesisId is > 0 ? candidateTesisId : null;
    }

    private static Func<IQueryable<Depo>, IQueryable<Depo>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Depo>, IQueryable<Depo>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }

            return result;
        };
    }
}
