using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariKartlar.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariHareketler.Services;

public class CariHareketService : BaseRdbmsService<CariHareketDto, CariHareket, int>, ICariHareketService
{
    private readonly ICariHareketRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public CariHareketService(ICariHareketRepository repository, ICariKartRepository cariKartRepository, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<CariEkstreDto> GetEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default)
    {
        var cari = await _cariKartRepository.GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && (!cari.TesisId.HasValue || !scope.TesisIds.Contains(cari.TesisId.Value)))
        {
            throw new BaseException("Bu kayit icin yetkiniz bulunmuyor.", 403);
        }
        var hareketler = await _repository.GetCariEkstresiAsync(cariKartId, baslangic, bitis, cancellationToken);
        var dtoHareketler = Mapper.Map<List<CariHareketDto>>(hareketler);
        return new CariEkstreDto
        {
            CariKartId = cari.Id,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.BorcTutari),
            ToplamAlacak = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.AlacakTutari),
            Bakiye = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).Sum(x => x.BorcTutari - x.AlacakTutari),
            Hareketler = dtoHareketler
        };
    }

    public override async Task<CariHareketDto> AddAsync(CariHareketDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<CariHareketDto> UpdateAsync(CariHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari hareket id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.Durum);
        return await base.UpdateAsync(dto);
    }

    public override async Task<CariHareketDto?> GetByIdAsync(int id, Func<IQueryable<CariHareket>, IQueryable<CariHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<CariHareketDto>> GetAllAsync(Func<IQueryable<CariHareket>, IQueryable<CariHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<CariHareketDto>> WhereAsync(System.Linq.Expressions.Expression<Func<CariHareket, bool>> predicate, Func<IQueryable<CariHareket>, IQueryable<CariHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<CariHareketDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<CariHareket, bool>>? predicate = null,
        Func<IQueryable<CariHareket>, IQueryable<CariHareket>>? include = null,
        Func<IQueryable<CariHareket>, IOrderedQueryable<CariHareket>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task ValidateAsync(int cariKartId, string durum)
    {
        if (cariKartId <= 0)
        {
            throw new BaseException("Cari secimi zorunludur.", 400);
        }

        var cari = await _cariKartRepository.GetByIdAsync(cariKartId);
        if (cari is null)
        {
            throw new BaseException("Cari kart bulunamadi.", 400);
        }
        if (!cari.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Seçilen cari kartın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            var tesisId = await _cariKartRepository.Where(x => x.Id == cariKartId).Select(x => x.TesisId).FirstOrDefaultAsync();
            if (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value))
            {
                throw new BaseException("Secilen cari kart icin yetkiniz bulunmuyor.", 403);
            }
        }

        if (durum != CariHareketDurumlari.Aktif && durum != CariHareketDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }
    }

    private static Func<IQueryable<CariHareket>, IQueryable<CariHareket>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<CariHareket>, IQueryable<CariHareket>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x =>
                    x.CariKart != null
                    && x.CariKart.TesisId.HasValue
                    && scope.TesisIds.Contains(x.CariKart.TesisId.Value));
            }

            return result;
        };
    }
}
