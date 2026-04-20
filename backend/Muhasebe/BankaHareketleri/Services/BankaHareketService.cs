using AutoMapper;
using STYS.AccessScope;
using STYS.Muhasebe.BankaHareketleri.Dtos;
using STYS.Muhasebe.BankaHareketleri.Entities;
using STYS.Muhasebe.BankaHareketleri.Repositories;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using STYS.Muhasebe.KasaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.BankaHareketleri.Services;

public class BankaHareketService : BaseRdbmsService<BankaHareketDto, BankaHareket, int>, IBankaHareketService
{
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IKasaBankaHesapRepository _kasaBankaHesapRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public BankaHareketService(IBankaHareketRepository repository, ICariKartRepository cariKartRepository, IKasaBankaHesapRepository kasaBankaHesapRepository, IUserAccessScopeService userAccessScopeService, IMapper mapper)
        : base(repository, mapper)
    {
        _cariKartRepository = cariKartRepository;
        _kasaBankaHesapRepository = kasaBankaHesapRepository;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<BankaHareketDto> AddAsync(BankaHareketDto dto)
    {
        await ValidateAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<BankaHareketDto> UpdateAsync(BankaHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Banka hareketi id zorunludur.", 400);
        }

        await ValidateAsync(dto);
        return await base.UpdateAsync(dto);
    }

    public override async Task<BankaHareketDto?> GetByIdAsync(int id, Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<BankaHareketDto>> GetAllAsync(Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<BankaHareketDto>> WhereAsync(System.Linq.Expressions.Expression<Func<BankaHareket, bool>> predicate, Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<BankaHareketDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<BankaHareket, bool>>? predicate = null,
        Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>>? include = null,
        Func<IQueryable<BankaHareket>, IOrderedQueryable<BankaHareket>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task ValidateAsync(BankaHareketDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.HareketTipi) || !new[] { KasaHareketTipleri.Tahsilat, KasaHareketTipleri.Odeme, KasaHareketTipleri.Devir, KasaHareketTipleri.Duzeltme }.Contains(dto.HareketTipi))
        {
            throw new BaseException("Hareket tipi gecersiz.", 400);
        }

        if (dto.Durum != CariHareketDurumlari.Aktif && dto.Durum != CariHareketDurumlari.Iptal)
        {
            throw new BaseException("Durum gecersiz.", 400);
        }

        if (dto.CariKartId.HasValue && dto.CariKartId.Value > 0)
        {
            var exists = await _cariKartRepository.AnyAsync(x => x.Id == dto.CariKartId.Value);
            if (!exists)
            {
                throw new BaseException("Cari kart bulunamadi.", 400);
            }
        }

        if (dto.KasaBankaHesapId.HasValue && dto.KasaBankaHesapId.Value > 0)
        {
            var hesap = await _kasaBankaHesapRepository.GetByIdAsync(dto.KasaBankaHesapId.Value);
            if (hesap is null || !hesap.AktifMi)
            {
                throw new BaseException("Secilen banka hesabi bulunamadi veya pasif.", 400);
            }

            if (hesap.Tip != KasaBankaHesapTipleri.Banka)
            {
                throw new BaseException("Secilen hesap banka tipinde degil.", 400);
            }

            dto.BankaAdi = hesap.BankaAdi ?? hesap.Ad;
            dto.HesapKoduIban = !string.IsNullOrWhiteSpace(hesap.Iban)
                ? hesap.Iban!
                : (hesap.HesapNo ?? hesap.Kod);
        }
        else if (string.IsNullOrWhiteSpace(dto.BankaAdi) || string.IsNullOrWhiteSpace(dto.HesapKoduIban))
        {
            throw new BaseException("Banka adi ve hesap/IBAN veya hesap secimi zorunludur.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            if (!dto.KasaBankaHesapId.HasValue || dto.KasaBankaHesapId.Value <= 0)
            {
                throw new BaseException("Scoped kullanicilar icin banka hesabi secimi zorunludur.", 400);
            }

            var scopedHesap = await _kasaBankaHesapRepository.GetByIdAsync(dto.KasaBankaHesapId.Value);
            if (scopedHesap?.TesisId is null || !scope.TesisIds.Contains(scopedHesap.TesisId.Value))
            {
                throw new BaseException("Secilen banka hesabi icin yetkiniz bulunmuyor.", 403);
            }
        }
    }

    private static Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<BankaHareket>, IQueryable<BankaHareket>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x =>
                    x.KasaBankaHesap != null
                    && x.KasaBankaHesap.TesisId.HasValue
                    && scope.TesisIds.Contains(x.KasaBankaHesap.TesisId.Value));
            }

            return result;
        };
    }
}
