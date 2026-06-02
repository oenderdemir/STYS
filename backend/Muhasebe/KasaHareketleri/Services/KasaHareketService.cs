using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaHareketleri.Dtos;
using STYS.Muhasebe.KasaHareketleri.Entities;
using STYS.Muhasebe.KasaHareketleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.KasaHareketleri.Services;

public class KasaHareketService : BaseRdbmsService<KasaHareketDto, KasaHareket, int>, IKasaHareketService
{
    private readonly IKasaHareketRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IKasaBankaHesapRepository _kasaBankaHesapRepository;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public KasaHareketService(
        IKasaHareketRepository repository,
        ICariKartRepository cariKartRepository,
        IKasaBankaHesapRepository kasaBankaHesapRepository,
        IMuhasebeDonemService muhasebeDonemService,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
        _kasaBankaHesapRepository = kasaBankaHesapRepository;
        _muhasebeDonemService = muhasebeDonemService;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<KasaHareketDto> AddAsync(KasaHareketDto dto)
    {
        await ValidateAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<KasaHareketDto> UpdateAsync(KasaHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kasa hareketi id zorunludur.", 400);
        }

        var visible = await GetByIdAsync(dto.Id.Value);
        if (visible is null)
        {
            throw new BaseException("Kasa hareketi bulunamadı.", 404);
        }

        var existing = await _repository.GetByIdAsync(dto.Id.Value)
            ?? throw new BaseException("Kasa hareketi bulunamadı.", 404);
        await EnsureOpenPeriodAsync(await ResolveTesisIdAsync(existing.KasaBankaHesapId, existing.CariKartId), existing.HareketTarihi, CancellationToken.None);

        await ValidateAsync(dto);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var visible = await GetByIdAsync(id);
        if (visible is null)
        {
            throw new BaseException("Kasa hareketi bulunamadı.", 404);
        }

        var existing = await _repository.GetByIdAsync(id)
            ?? throw new BaseException("Kasa hareketi bulunamadı.", 404);

        await EnsureOpenPeriodAsync(await ResolveTesisIdAsync(existing.KasaBankaHesapId, existing.CariKartId), existing.HareketTarihi, CancellationToken.None);
        await base.DeleteAsync(id);
    }

    public override async Task<KasaHareketDto?> GetByIdAsync(int id, Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<KasaHareketDto>> GetAllAsync(Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<KasaHareketDto>> WhereAsync(System.Linq.Expressions.Expression<Func<KasaHareket, bool>> predicate, Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<KasaHareketDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<KasaHareket, bool>>? predicate = null,
        Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>>? include = null,
        Func<IQueryable<KasaHareket>, IOrderedQueryable<KasaHareket>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task ValidateAsync(KasaHareketDto dto)
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
            var cari = await _cariKartRepository.GetByIdAsync(dto.CariKartId.Value);
            if (cari is null)
            {
                throw new BaseException("Cari kart bulunamadi.", 400);
            }
            if (!cari.MuhasebeHesapPlaniId.HasValue)
            {
                throw new BaseException("Seçilen cari kartın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
            }
        }

        if (dto.KasaBankaHesapId.HasValue && dto.KasaBankaHesapId.Value > 0)
        {
            var hesap = await _kasaBankaHesapRepository.GetByIdAsync(dto.KasaBankaHesapId.Value);
            if (hesap is null || !hesap.AktifMi)
            {
                throw new BaseException("Secilen kasa hesabi bulunamadi veya pasif.", 400);
            }

            if (hesap.Tip != KasaBankaHesapTipleri.NakitKasa)
            {
                throw new BaseException("Secilen hesap kasa tipinde degil.", 400);
            }
            if (!hesap.MuhasebeHesapPlaniId.HasValue)
            {
                throw new BaseException("Seçilen finansal hesabın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
            }

            dto.KasaKodu = hesap.Kod;
        }
        else if (string.IsNullOrWhiteSpace(dto.KasaKodu))
        {
            throw new BaseException("Kasa kodu veya hesap secimi zorunludur.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            if (!dto.KasaBankaHesapId.HasValue || dto.KasaBankaHesapId.Value <= 0)
            {
                throw new BaseException("Scoped kullanicilar icin kasa hesabi secimi zorunludur.", 400);
            }

            var scopedHesap = await _kasaBankaHesapRepository.GetByIdAsync(dto.KasaBankaHesapId.Value);
            if (scopedHesap?.TesisId is null || !scope.TesisIds.Contains(scopedHesap.TesisId.Value))
            {
                throw new BaseException("Secilen kasa hesabi icin yetkiniz bulunmuyor.", 403);
            }
        }

        await EnsureOpenPeriodAsync(await ResolveTesisIdAsync(dto.KasaBankaHesapId, dto.CariKartId), dto.HareketTarihi, CancellationToken.None);
    }

    private static Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<KasaHareket>, IQueryable<KasaHareket>>? include)
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

    private async Task<int?> ResolveTesisIdAsync(int? kasaBankaHesapId, int? cariKartId)
    {
        if (kasaBankaHesapId.HasValue && kasaBankaHesapId.Value > 0)
        {
            return await _kasaBankaHesapRepository.Where(x => x.Id == kasaBankaHesapId.Value).Select(x => x.TesisId).FirstOrDefaultAsync();
        }

        if (cariKartId.HasValue && cariKartId.Value > 0)
        {
            return await _cariKartRepository.Where(x => x.Id == cariKartId.Value).Select(x => x.TesisId).FirstOrDefaultAsync();
        }

        return null;
    }

    private async Task EnsureOpenPeriodAsync(int? tesisId, DateTime tarih, CancellationToken cancellationToken)
    {
        await MuhasebeDonemKontrolHelper.EnsureOpenPeriodAsync(_muhasebeDonemService, tesisId, tarih, cancellationToken);
    }
}
