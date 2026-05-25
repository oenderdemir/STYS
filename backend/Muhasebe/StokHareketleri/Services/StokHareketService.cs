using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokHareketleri.Services;

public class StokHareketService : BaseRdbmsService<StokHareketDto, StokHareket, int>, IStokHareketService
{
    private readonly IStokHareketRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IKdvUygulamaService _kdvUygulamaService;

    public StokHareketService(
        IStokHareketRepository repository,
        IDepoRepository depoRepository,
        ITasinirKartRepository tasinirKartRepository,
        ICariKartRepository cariKartRepository,
        IUserAccessScopeService userAccessScopeService,
        IKdvUygulamaService kdvUygulamaService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _depoRepository = depoRepository;
        _tasinirKartRepository = tasinirKartRepository;
        _cariKartRepository = cariKartRepository;
        _userAccessScopeService = userAccessScopeService;
        _kdvUygulamaService = kdvUygulamaService;
    }

    public override async Task<StokHareketDto> AddAsync(StokHareketDto dto)
    {
        await NormalizeAndValidateAsync(dto, null);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        await ApplyKdvAsync(dto);
        return await base.AddAsync(dto);
    }

    public override async Task<StokHareketDto> UpdateAsync(StokHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Stok hareket id zorunludur.", 400);
        }

        await NormalizeAndValidateAsync(dto, dto.Id);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        await ApplyKdvAsync(dto);
        return await base.UpdateAsync(dto);
    }

    public async Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default)
    {
        var allowedDepoIds = await ResolveAllowedDepoIdsAsync(tesisId, cancellationToken);
        if (allowedDepoIds is not null && allowedDepoIds.Count == 0)
        {
            return [];
        }

        if (depoId.HasValue && depoId.Value > 0)
        {
            if (allowedDepoIds is not null && !allowedDepoIds.Contains(depoId.Value))
            {
                return [];
            }

            return await _repository.GetDepoStokBakiyeleriAsync(new[] { depoId.Value }, cancellationToken);
        }

        var result = await _repository.GetDepoStokBakiyeleriAsync(allowedDepoIds, cancellationToken);
        return result;
    }

    public async Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? tesisId, int? depoId, CancellationToken cancellationToken = default)
    {
        var allowedDepoIds = await ResolveAllowedDepoIdsAsync(tesisId, cancellationToken);
        if (allowedDepoIds is not null && allowedDepoIds.Count == 0)
        {
            return [];
        }

        if (depoId.HasValue && depoId.Value > 0)
        {
            if (allowedDepoIds is not null && !allowedDepoIds.Contains(depoId.Value))
            {
                return [];
            }

            return await _repository.GetStokKartOzetleriAsync(new[] { depoId.Value }, cancellationToken);
        }

        var result = await _repository.GetStokKartOzetleriAsync(allowedDepoIds, cancellationToken);
        return result;
    }

    private async Task<HashSet<int>?> ResolveAllowedDepoIdsAsync(int? tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped && (!tesisId.HasValue || tesisId.Value <= 0))
        {
            return null;
        }

        var query = _depoRepository.Where(x => x.TesisId.HasValue);
        if (scope.IsScoped)
        {
            query = query.Where(x => x.TesisId.HasValue && scope.TesisIds.Contains(x.TesisId.Value));
        }
        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        var depoIds = await query.Select(x => x.Id).ToListAsync(cancellationToken);
        return depoIds.ToHashSet();
    }

    public override async Task<StokHareketDto?> GetByIdAsync(int id, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<StokHareketDto>> GetAllAsync(Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<StokHareketDto>> WhereAsync(System.Linq.Expressions.Expression<Func<StokHareket, bool>> predicate, Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<StokHareketDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        System.Linq.Expressions.Expression<Func<StokHareket, bool>>? predicate = null,
        Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include = null,
        Func<IQueryable<StokHareket>, IOrderedQueryable<StokHareket>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task NormalizeAndValidateAsync(StokHareketDto dto, int? currentId)
    {
        dto.HareketTipi = dto.HareketTipi?.Trim() ?? string.Empty;
        dto.Durum = dto.Durum?.Trim() ?? string.Empty;
        dto.BelgeNo = NormalizeOptional(dto.BelgeNo);
        dto.Aciklama = NormalizeOptional(dto.Aciklama);
        dto.KaynakModul = NormalizeOptional(dto.KaynakModul);

        if (dto.DepoId <= 0 || !await _depoRepository.AnyAsync(x => x.Id == dto.DepoId))
        {
            throw new BaseException("Gecerli bir depo secilmelidir.", 400);
        }
        var depo = await _depoRepository.GetByIdAsync(dto.DepoId);
        if (depo is null || !depo.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Seçilen deponun muhasebe hesap planı bağlantısı bulunmuyor.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            var depoTesisId = await _depoRepository.Where(x => x.Id == dto.DepoId).Select(x => x.TesisId).FirstOrDefaultAsync();
            if (!depoTesisId.HasValue || !scope.TesisIds.Contains(depoTesisId.Value))
            {
                throw new BaseException("Secilen depo icin yetkiniz bulunmuyor.", 403);
            }
        }

        if (dto.TasinirKartId <= 0 || !await _tasinirKartRepository.AnyAsync(x => x.Id == dto.TasinirKartId))
        {
            throw new BaseException("Gecerli bir tasinir kart secilmelidir.", 400);
        }
        var tasinirKart = await _tasinirKartRepository.GetByIdAsync(dto.TasinirKartId);
        if (tasinirKart is null || !tasinirKart.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Seçilen taşınır kartın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
        }

        if (dto.CariKartId.HasValue && dto.CariKartId.Value > 0)
        {
            var cari = await _cariKartRepository.GetByIdAsync(dto.CariKartId.Value);
            if (cari is null)
            {
                throw new BaseException("Secilen cari kart bulunamadi.", 400);
            }
            if (!cari.MuhasebeHesapPlaniId.HasValue)
            {
                throw new BaseException("Seçilen cari kartın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
            }
        }

        if (!StokHareketTipleri.Hepsi.Contains(dto.HareketTipi))
        {
            throw new BaseException("Hareket tipi gecersiz.", 400);
        }

        if (!StokHareketDurumlari.Hepsi.Contains(dto.Durum))
        {
            throw new BaseException("Durum gecersiz.", 400);
        }

        if (dto.Miktar <= 0)
        {
            throw new BaseException("Miktar 0'dan buyuk olmalidir.", 400);
        }

        if (dto.BirimFiyat < 0)
        {
            throw new BaseException("Birim fiyat negatif olamaz.", 400);
        }

        if (dto.HareketTarihi == default)
        {
            dto.HareketTarihi = DateTime.UtcNow;
        }
    }

    private async Task ApplyKdvAsync(StokHareketDto dto)
    {
        var islemYonu = StokHareketTipleri.CikisEtkisi.Contains(dto.HareketTipi)
            ? KdvIslemYonu.Satis
            : KdvIslemYonu.Alis;

        var result = await _kdvUygulamaService.ValidateAndSnapshotAsync(
            dto.KdvUygulamaTipi,
            dto.KdvIstisnaTanimId,
            dto.KdvOrani,
            dto.Tutar,
            dto.HareketTarihi,
            islemYonu);

        dto.KdvUygulamaTipi = result.KdvUygulamaTipi;
        dto.KdvIstisnaTanimId = result.KdvIstisnaTanimId;
        dto.KdvIstisnaKodu = result.KdvIstisnaKodu;
        dto.KdvIstisnaAciklamasi = result.KdvIstisnaAciklamasi;
        dto.KdvOrani = result.KdvOrani;
        dto.KdvTutari = result.KdvTutari;
    }

    private static decimal CalculateTutar(decimal miktar, decimal birimFiyat)
        => Math.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Func<IQueryable<StokHareket>, IQueryable<StokHareket>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<StokHareket>, IQueryable<StokHareket>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            if (scope.IsScoped)
            {
                result = result.Where(x =>
                    x.Depo != null
                    && x.Depo.TesisId.HasValue
                    && scope.TesisIds.Contains(x.Depo.TesisId.Value));
            }

            return result;
        };
    }
}
