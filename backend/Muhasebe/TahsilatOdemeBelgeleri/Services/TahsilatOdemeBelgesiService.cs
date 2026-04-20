using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

public class TahsilatOdemeBelgesiService : BaseRdbmsService<TahsilatOdemeBelgesiDto, TahsilatOdemeBelgesi, int>, ITahsilatOdemeBelgesiService
{
    private readonly ITahsilatOdemeBelgesiRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public TahsilatOdemeBelgesiService(
        ITahsilatOdemeBelgesiRepository repository,
        ICariKartRepository cariKartRepository,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetGunlukAsync(gun, cancellationToken);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            var scopedCariIds = await _cariKartRepository
                .Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var scopedCariIdSet = scopedCariIds.ToHashSet();
            list = list.Where(x => scopedCariIdSet.Contains(x.CariKartId)).ToList();
        }

        var aktifler = list.Where(x => x.Durum == TahsilatOdemeBelgeDurumlari.Aktif).ToList();
        var tahsilat = aktifler.Where(x => x.BelgeTipi == TahsilatOdemeBelgeTipleri.Tahsilat).Sum(x => x.Tutar);
        var odeme = aktifler.Where(x => x.BelgeTipi == TahsilatOdemeBelgeTipleri.Odeme).Sum(x => x.Tutar);

        return new TahsilatOdemeOzetDto
        {
            Gun = gun.Date,
            ToplamTahsilat = tahsilat,
            ToplamOdeme = odeme,
            Net = tahsilat - odeme,
            ParaBirimi = aktifler.FirstOrDefault()?.ParaBirimi ?? "TRY"
        };
    }

    public override async Task<TahsilatOdemeBelgesiDto> AddAsync(TahsilatOdemeBelgesiDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.BelgeTipi, dto.OdemeYontemi, dto.Durum);
        return await base.AddAsync(dto);
    }

    public override async Task<TahsilatOdemeBelgesiDto> UpdateAsync(TahsilatOdemeBelgesiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tahsilat/odeme belgesi id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.BelgeTipi, dto.OdemeYontemi, dto.Durum);
        return await base.UpdateAsync(dto);
    }

    public override async Task<TahsilatOdemeBelgesiDto?> GetByIdAsync(int id, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<TahsilatOdemeBelgesiDto>> GetAllAsync(Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<TahsilatOdemeBelgesiDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>> predicate, Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<TahsilatOdemeBelgesiDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<TahsilatOdemeBelgesi, bool>>? predicate = null,
        Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include = null,
        Func<IQueryable<TahsilatOdemeBelgesi>, IOrderedQueryable<TahsilatOdemeBelgesi>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task ValidateAsync(int cariKartId, string belgeTipi, string odemeYontemi, string durum)
    {
        if (cariKartId <= 0)
        {
            throw new BaseException("Cari kart secimi zorunludur.", 400);
        }

        var cariExists = await _cariKartRepository.AnyAsync(x => x.Id == cariKartId);
        if (!cariExists)
        {
            throw new BaseException("Cari kart bulunamadi.", 400);
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

        if (belgeTipi != TahsilatOdemeBelgeTipleri.Tahsilat && belgeTipi != TahsilatOdemeBelgeTipleri.Odeme)
        {
            throw new BaseException("Belge tipi gecersiz.", 400);
        }

        if (!OdemeYontemleri.Hepsi.Contains(odemeYontemi))
        {
            throw new BaseException("Odeme yontemi gecersiz.", 400);
        }

        if (durum != TahsilatOdemeBelgeDurumlari.Aktif && durum != TahsilatOdemeBelgeDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }
    }

    private static Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<TahsilatOdemeBelgesi>, IQueryable<TahsilatOdemeBelgesi>>? include)
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
