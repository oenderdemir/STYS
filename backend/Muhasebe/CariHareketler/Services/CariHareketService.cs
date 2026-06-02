using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.CariHareketler.Dtos;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariHareketler.Repositories;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.CariHareketler.Services;

public class CariHareketService : BaseRdbmsService<CariHareketDto, CariHareket, int>, ICariHareketService
{
    private readonly ICariHareketRepository _repository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public CariHareketService(
        ICariHareketRepository repository,
        ICariKartRepository cariKartRepository,
        IMuhasebeDonemService muhasebeDonemService,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _repository = repository;
        _cariKartRepository = cariKartRepository;
        _muhasebeDonemService = muhasebeDonemService;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<CariEkstreDto> GetEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default)
    {
        var (cari, hareketler) = await GetScopedCariHareketlerAsync(cariKartId, baslangic, bitis, cancellationToken);
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

    public async Task<CariBakiyeOzetDto> GetCariBakiyeOzetAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var (cari, hareketler) = await GetScopedCariHareketlerAsync(cariKartId, null, null, cancellationToken);
        var aktifHareketler = hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif).ToList();
        var toplamBorc = aktifHareketler.Sum(x => x.BorcTutari);
        var toplamAlacak = aktifHareketler.Sum(x => x.AlacakTutari);
        var bakiye = toplamBorc - toplamAlacak;
        var acikHareketler = aktifHareketler.Where(x => !x.KapandiMi && x.KalanTutar > 0m).ToList();

        return new CariBakiyeOzetDto
        {
            CariKartId = cari.Id,
            CariKodu = cari.CariKodu,
            UnvanAdSoyad = cari.UnvanAdSoyad,
            ToplamBorc = toplamBorc,
            ToplamAlacak = toplamAlacak,
            Bakiye = bakiye,
            BakiyeYonu = bakiye > 0m ? "Borclu" : bakiye < 0m ? "Alacakli" : "Sifir",
            ToplamAcikBorc = acikHareketler.Where(x => x.BorcTutari > 0m).Sum(x => x.KalanTutar),
            ToplamAcikAlacak = acikHareketler.Where(x => x.AlacakTutari > 0m).Sum(x => x.KalanTutar),
            AcikHareketSayisi = acikHareketler.Count,
            KapananHareketSayisi = aktifHareketler.Count(x => x.KapandiMi)
        };
    }

    public async Task<List<CariHareketDurumOzetDto>> GetCariAcikHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var (_, hareketler) = await GetScopedCariHareketlerAsync(cariKartId, null, null, cancellationToken);
        return Mapper.Map<List<CariHareketDurumOzetDto>>(
            hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif && !x.KapandiMi && x.KalanTutar > 0m)
                .OrderByDescending(x => x.HareketTarihi)
                .ThenByDescending(x => x.Id)
                .ToList());
    }

    public async Task<List<CariHareketDurumOzetDto>> GetCariKapananHareketlerAsync(int cariKartId, CancellationToken cancellationToken = default)
    {
        var (_, hareketler) = await GetScopedCariHareketlerAsync(cariKartId, null, null, cancellationToken);
        return Mapper.Map<List<CariHareketDurumOzetDto>>(
            hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif && x.KapandiMi)
                .OrderByDescending(x => x.HareketTarihi)
                .ThenByDescending(x => x.Id)
                .ToList());
    }

    public async Task<List<CariHareketDurumOzetDto>> GetCariHareketEkstreAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default)
    {
        var (_, hareketler) = await GetScopedCariHareketlerAsync(cariKartId, baslangic, bitis, cancellationToken);
        return Mapper.Map<List<CariHareketDurumOzetDto>>(
            hareketler.Where(x => x.Durum == CariHareketDurumlari.Aktif)
                .OrderByDescending(x => x.HareketTarihi)
                .ThenByDescending(x => x.Id)
                .ToList());
    }

    public override async Task<CariHareketDto> AddAsync(CariHareketDto dto)
    {
        await ValidateAsync(dto.CariKartId, dto.Durum, dto.HareketTarihi);
        return await base.AddAsync(dto);
    }

    public override async Task<CariHareketDto> UpdateAsync(CariHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Cari hareket id zorunludur.", 400);
        }

        var visible = await GetByIdAsync(dto.Id.Value);
        if (visible is null)
        {
            throw new BaseException("Cari hareket bulunamadı.", 404);
        }

        var existing = await _repository.GetByIdAsync(dto.Id.Value)
            ?? throw new BaseException("Cari hareket bulunamadı.", 404);

        await EnsureOpenPeriodAsync(existing.CariKartId, existing.HareketTarihi, CancellationToken.None);
        await ValidateAsync(dto.CariKartId, dto.Durum, dto.HareketTarihi);
        return await base.UpdateAsync(dto);
    }

    public override async Task DeleteAsync(int id)
    {
        var visible = await GetByIdAsync(id);
        if (visible is null)
        {
            throw new BaseException("Cari hareket bulunamadı.", 404);
        }

        var existing = await _repository.GetByIdAsync(id)
            ?? throw new BaseException("Cari hareket bulunamadı.", 404);

        await EnsureOpenPeriodAsync(existing.CariKartId, existing.HareketTarihi, CancellationToken.None);
        await base.DeleteAsync(id);
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

    protected override Task EnrichEntityAsync(CariHareketDto dto, CariHareket entity)
    {
        if (entity.KalanTutar <= 0m && entity.KapananTutar <= 0m)
        {
            var hareketTutari = entity.BorcTutari > 0 ? entity.BorcTutari : entity.AlacakTutari;
            entity.KapananTutar = 0m;
            entity.KalanTutar = hareketTutari;
            entity.KapandiMi = false;
            entity.IliskiliCariHareketId = null;
        }

        return Task.CompletedTask;
    }

    private async Task ValidateAsync(int cariKartId, string durum, DateTime hareketTarihi)
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

        await EnsureOpenPeriodAsync(cari.TesisId, hareketTarihi, CancellationToken.None);
    }

    private async Task EnsureOpenPeriodAsync(int? tesisId, DateTime tarih, CancellationToken cancellationToken)
    {
        await MuhasebeDonemKontrolHelper.EnsureOpenPeriodAsync(_muhasebeDonemService, tesisId, tarih, cancellationToken);
    }

    private async Task<(STYS.Muhasebe.CariKartlar.Entities.CariKart Cari, List<CariHareket> Hareketler)> GetScopedCariHareketlerAsync(
        int cariKartId,
        DateTime? baslangic,
        DateTime? bitis,
        CancellationToken cancellationToken)
    {
        var cari = await _cariKartRepository.GetByIdAsync(cariKartId) ?? throw new BaseException("Cari kart bulunamadi.", 404);
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && (!cari.TesisId.HasValue || !scope.TesisIds.Contains(cari.TesisId.Value)))
        {
            throw new BaseException("Bu kayit icin yetkiniz bulunmuyor.", 403);
        }

        var query = _repository.Where(x => x.CariKartId == cariKartId && !x.IsDeleted);
        if (baslangic.HasValue)
        {
            query = query.Where(x => x.HareketTarihi >= baslangic.Value.Date);
        }

        if (bitis.HasValue)
        {
            var bitisDate = bitis.Value.Date.AddDays(1);
            query = query.Where(x => x.HareketTarihi < bitisDate);
        }

        var hareketler = await query
            .OrderBy(x => x.HareketTarihi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return (cari, hareketler);
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
