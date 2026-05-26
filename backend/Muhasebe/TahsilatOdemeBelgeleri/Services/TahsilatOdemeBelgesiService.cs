using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariHareketler.Services;
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
    private readonly ICariHareketRepository _cariHareketRepository;
    private readonly ICariHareketKapamaService _cariHareketKapamaService;
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public TahsilatOdemeBelgesiService(
        ITahsilatOdemeBelgesiRepository repository,
        ICariKartRepository cariKartRepository,
        ICariHareketRepository cariHareketRepository,
        ICariHareketKapamaService cariHareketKapamaService,
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
        _cariHareketRepository = cariHareketRepository;
        _cariHareketKapamaService = cariHareketKapamaService;
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, int? tesisId, CancellationToken cancellationToken = default)
    {
        var list = await _repository.GetGunlukAsync(gun, cancellationToken);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped || (tesisId.HasValue && tesisId.Value > 0))
        {
            var cariQuery = _cariKartRepository.Where(x => x.TesisId.HasValue);
            if (scope.IsScoped)
            {
                cariQuery = cariQuery.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
            }
            if (tesisId.HasValue && tesisId.Value > 0)
            {
                cariQuery = cariQuery.Where(x => x.TesisId == tesisId.Value);
            }

            var scopedCariIds = await cariQuery.Select(x => x.Id).ToListAsync(cancellationToken);
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
        await ValidateKapatilacakCariHareketAsync(dto.CariKartId, dto.KapatilacakCariHareketId);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var entity = Mapper.Map<TahsilatOdemeBelgesi>(dto);
            await _repository.AddAsync(entity);
            await _repository.SaveChangesAsync();

            if (dto.KapatilacakCariHareketId.HasValue)
            {
                await _cariHareketKapamaService.TahsilatOdemeIcinCariHareketOlusturVeKapatAsync(entity.Id, CancellationToken.None);
            }

            await transaction.CommitAsync();
            return Mapper.Map<TahsilatOdemeBelgesiDto>(entity);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public override async Task<TahsilatOdemeBelgesiDto> UpdateAsync(TahsilatOdemeBelgesiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tahsilat/odeme belgesi id zorunludur.", 400);
        }

        await ValidateAsync(dto.CariKartId, dto.BelgeTipi, dto.OdemeYontemi, dto.Durum);
        await ValidateKapatilacakCariHareketAsync(dto.CariKartId, dto.KapatilacakCariHareketId);

        if (await HasCariHareketAsync(dto.Id.Value))
        {
            throw new BaseException("Cari kapama yapılmış tahsilat/ödeme belgesi güncellenemez.", 400);
        }

        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        if (await HasCariHareketAsync(id))
        {
            throw new BaseException("Cari kapama yapılmış tahsilat/ödeme belgesi silinemez.", 400);
        }

        await base.DeleteAsync(id);
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

    private async Task ValidateKapatilacakCariHareketAsync(int cariKartId, int? kapatilacakCariHareketId)
    {
        if (!kapatilacakCariHareketId.HasValue)
        {
            return;
        }

        var hareket = await _cariHareketRepository
            .Where(x => x.Id == kapatilacakCariHareketId.Value)
            .Include(x => x.CariKart)
            .FirstOrDefaultAsync();

        if (hareket is null)
        {
            throw new BaseException("Kapatilacak cari hareket bulunamadi.", 400);
        }

        if (hareket.IsDeleted || hareket.Durum != CariHareketDurumlari.Aktif)
        {
            throw new BaseException("Kapatilacak cari hareket aktif degil.", 400);
        }

        if (hareket.KapandiMi || hareket.KalanTutar <= 0m)
        {
            throw new BaseException("Kapatilacak cari hareket kapali.", 400);
        }

        if (hareket.CariKartId != cariKartId)
        {
            throw new BaseException("Kapatilacak cari hareket secilen cari kart ile uyumlu degil.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            var cariTesisId = hareket.CariKart?.TesisId;
            if (!cariTesisId.HasValue || !scope.TesisIds.Contains(cariTesisId.Value))
            {
                throw new BaseException("Kapatilacak cari hareket icin yetkiniz bulunmuyor.", 403);
            }
        }
    }

    private async Task<bool> HasCariHareketAsync(int tahsilatOdemeBelgesiId)
    {
        return await _dbContext.CariHareketler.AnyAsync(x =>
            !x.IsDeleted
            && x.Durum == CariHareketDurumlari.Aktif
            && x.KaynakModul == "TahsilatOdemeBelgesi"
            && x.KaynakId == tahsilatOdemeBelgesiId);
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
