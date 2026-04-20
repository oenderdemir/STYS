using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariKartlar.Services;

public class CariKartService : BaseRdbmsService<CariKartDto, CariKart, int>, ICariKartService
{
    private readonly ICariKartRepository _repository;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public CariKartService(ICariKartRepository repository, StysAppDbContext dbContext, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var cari = await GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        await EnsureCanAccessTesisAsync(cari.TesisId, cancellationToken);
        var hareketler = await _dbContext.CariHareketler
            .Where(x => x.CariKartId == cariKartId && x.Durum == CariHareketDurumlari.Aktif)
            .ToListAsync(cancellationToken);

        var toplamBorc = hareketler.Sum(x => x.BorcTutari);
        var toplamAlacak = hareketler.Sum(x => x.AlacakTutari);
        return new CariBakiyeDto
        {
            CariKartId = cariKartId,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Bakiye = toplamBorc - toplamAlacak,
            ParaBirimi = hareketler.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };
    }

    public override async Task<CariKartDto> AddAsync(CariKartDto dto)
    {
        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, null);
        Normalize(dto);
        var normalizedCode = dto.CariKodu.Trim().ToUpperInvariant();
        var exists = await _repository.AnyAsync(x => x.CariKodu.ToUpper() == normalizedCode && x.TesisId == dto.TesisId);
        if (exists)
        {
            throw new BaseException("Cari kodu ayni tesis altinda benzersiz olmalidir.", 400);
        }

        dto.CariKodu = normalizedCode;
        dto.UnvanAdSoyad = dto.UnvanAdSoyad.Trim();
        dto.CariTipi = dto.CariTipi.Trim();
        return await base.AddAsync(dto);
    }

    public override async Task<CariKartDto> UpdateAsync(CariKartDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari kart id zorunludur.", 400);
        }

        dto.TesisId = await ResolveWriteTesisIdAsync(dto.TesisId, dto.Id.Value);
        Normalize(dto);
        var normalizedCode = dto.CariKodu.Trim().ToUpperInvariant();
        var exists = await _repository.AnyAsync(x => x.Id != dto.Id.Value && x.CariKodu.ToUpper() == normalizedCode && x.TesisId == dto.TesisId);
        if (exists)
        {
            throw new BaseException("Cari kodu ayni tesis altinda benzersiz olmalidir.", 400);
        }

        dto.CariKodu = normalizedCode;
        dto.UnvanAdSoyad = dto.UnvanAdSoyad.Trim();
        dto.CariTipi = dto.CariTipi.Trim();
        return await base.UpdateAsync(dto);
    }

    public override async Task<CariKartDto?> GetByIdAsync(int id, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<CariKartDto>> GetAllAsync(Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<CariKartDto>> WhereAsync(System.Linq.Expressions.Expression<Func<CariKart, bool>> predicate, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<CariKartDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<CariKart, bool>>? predicate = null,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null,
        Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private static void Normalize(CariKartDto dto)
    {
        if (!CariKartTipleri.Hepsi.Contains(dto.CariTipi))
        {
            throw new BaseException("Cari tipi gecersiz.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.CariKodu))
        {
            throw new BaseException("Cari kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.UnvanAdSoyad))
        {
            throw new BaseException("Unvan/Ad Soyad zorunludur.", 400);
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

        if (candidateTesisId.HasValue && candidateTesisId.Value > 0)
        {
            var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == candidateTesisId.Value && x.AktifMi);
            if (!tesisExists)
            {
                throw new BaseException("Secilen tesis bulunamadi.", 400);
            }
        }

        return candidateTesisId is > 0 ? candidateTesisId : null;
    }

    private async Task EnsureCanAccessTesisAsync(int? tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped)
        {
            return;
        }

        if (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value))
        {
            throw new BaseException("Bu kayda erisim yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<CariKart>, IQueryable<CariKart>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include)
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
