using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.AccessScope;
using STYS.Muhasebe.CariKartlar.Repositories;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.Kdv.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Repositories;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.Muhasebe.StokHareketleri.Services;

public class StokHareketService : BaseRdbmsService<StokHareketDto, StokHareket, int>, IStokHareketService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IStokHareketRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly ICariKartRepository _cariKartRepository;
    private readonly IMuhasebeDonemService _muhasebeDonemService;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IKdvUygulamaService _kdvUygulamaService;
    private readonly IMapper _mapper;

    public StokHareketService(
        StysAppDbContext dbContext,
        IStokHareketRepository repository,
        IDepoRepository depoRepository,
        ITasinirKartRepository tasinirKartRepository,
        ICariKartRepository cariKartRepository,
        IMuhasebeDonemService muhasebeDonemService,
        IUserAccessScopeService userAccessScopeService,
        IKdvUygulamaService kdvUygulamaService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _dbContext = dbContext;
        _repository = repository;
        _depoRepository = depoRepository;
        _tasinirKartRepository = tasinirKartRepository;
        _cariKartRepository = cariKartRepository;
        _muhasebeDonemService = muhasebeDonemService;
        _userAccessScopeService = userAccessScopeService;
        _kdvUygulamaService = kdvUygulamaService;
        _mapper = mapper;
    }

    public override async Task<StokHareketDto> AddAsync(StokHareketDto dto)
    {
        if (string.Equals(dto.HareketTipi, StokHareketTipleri.Transfer, StringComparison.Ordinal))
        {
            throw new BaseException("Transfer hareketleri yalnizca transfer olusturma akisi ile kaydedilebilir.", 400);
        }

        await NormalizeAndValidateAsync(dto, null);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        await ApplyKdvAsync(dto);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        try
        {
            await EnsureCreateDoesNotGoNegativeAsync(dto, CancellationToken.None);
            var created = await base.AddAsync(dto);
            await transaction.CommitAsync(CancellationToken.None);
            return created;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task<StokHareketDto> UpdateAsync(StokHareketDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Stok hareket id zorunludur.", 400);
        }

        var visible = await GetByIdAsync(dto.Id.Value);
        if (visible is null)
        {
            throw new BaseException("Stok hareket bulunamadı.", 404);
        }

        DetachTrackedStokHareket(dto.Id.Value);

        var existing = await GetExistingMovementSnapshotAsync(dto.Id.Value, CancellationToken.None)
            ?? throw new BaseException("Stok hareket bulunamadı.", 404);

        if (existing.TransferGrupId.HasValue)
        {
            throw new BaseException("Transfer kayitlari dogrudan guncellenemez. Transferi iptal edip yeniden olusturunuz.", 400);
        }

        var existingDepo = await GetDepoOrThrowAsync(existing.DepoId);
        await EnsureOpenPeriodAsync(existingDepo.TesisId, existing.HareketTarihi, CancellationToken.None);
        await NormalizeAndValidateAsync(dto, dto.Id);
        dto.Tutar = CalculateTutar(dto.Miktar, dto.BirimFiyat);
        await ApplyKdvAsync(dto);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        try
        {
            existing = await GetExistingMovementSnapshotAsync(dto.Id.Value, CancellationToken.None)
                ?? throw new BaseException("Stok hareket bulunamadı.", 404);

            await EnsureUpdateDoesNotGoNegativeAsync(existing, dto, CancellationToken.None);
            DetachTrackedStokHareket(dto.Id.Value);
            var updated = await base.UpdateAsync(dto);
            await transaction.CommitAsync(CancellationToken.None);
            return updated;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public override async Task DeleteAsync(int id)
    {
        var visible = await GetByIdAsync(id);
        if (visible is null)
        {
            throw new BaseException("Stok hareket bulunamadı.", 404);
        }

        DetachTrackedStokHareket(id);

        var existing = await GetExistingMovementSnapshotAsync(id, CancellationToken.None)
            ?? throw new BaseException("Stok hareket bulunamadı.", 404);

        if (existing.TransferGrupId.HasValue)
        {
            throw new BaseException("Transfer kayitlari dogrudan silinemez. Transferi iptal etme akisini kullaniniz.", 400);
        }

        var existingDepo = await GetDepoOrThrowAsync(existing.DepoId);
        await EnsureOpenPeriodAsync(existingDepo.TesisId, existing.HareketTarihi, CancellationToken.None);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        try
        {
            existing = await GetExistingMovementSnapshotAsync(id, CancellationToken.None)
                ?? throw new BaseException("Stok hareket bulunamadı.", 404);

            await EnsureDeleteDoesNotGoNegativeAsync(existing, CancellationToken.None);
            DetachTrackedStokHareket(id);
            await base.DeleteAsync(id);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<StokHareketDto>> CreateTransferAsync(StokTransferRequest request, CancellationToken cancellationToken = default)
    {
        NormalizeTransferRequest(request);

        if (request.KaynakDepoId <= 0 || request.HedefDepoId <= 0)
        {
            throw new BaseException("Kaynak depo ve hedef depo secimi zorunludur.", 400);
        }

        if (request.KaynakDepoId == request.HedefDepoId)
        {
            throw new BaseException("Kaynak depo ile hedef depo ayni olamaz.", 400);
        }

        if (request.TasinirKartId <= 0)
        {
            throw new BaseException("Gecerli bir tasinir kart secilmelidir.", 400);
        }

        if (request.Miktar <= 0)
        {
            throw new BaseException("Miktar 0'dan buyuk olmalidir.", 400);
        }

        if (request.BirimFiyat < 0)
        {
            throw new BaseException("Birim fiyat negatif olamaz.", 400);
        }

        if (request.HareketTarihi == default)
        {
            request.HareketTarihi = DateTime.UtcNow;
        }

        var kaynakDepo = await _depoRepository.GetByIdAsync(request.KaynakDepoId)
            ?? throw new BaseException("Kaynak depo bulunamadi.", 400);
        var hedefDepo = await _depoRepository.GetByIdAsync(request.HedefDepoId)
            ?? throw new BaseException("Hedef depo bulunamadi.", 400);
        var tasinirKart = await _tasinirKartRepository.GetByIdAsync(request.TasinirKartId)
            ?? throw new BaseException("Secilen tasinir kart bulunamadi.", 400);

        ValidateTransferDepo(kaynakDepo, "Kaynak depo");
        ValidateTransferDepo(hedefDepo, "Hedef depo");

        if (!kaynakDepo.TesisId.HasValue || !hedefDepo.TesisId.HasValue || kaynakDepo.TesisId.Value != hedefDepo.TesisId.Value)
        {
            throw new BaseException("Transfer yalnizca ayni tesise ait depolar arasinda yapilabilir.", 400);
        }

        if (!tasinirKart.AktifMi)
        {
            throw new BaseException("Secilen tasinir kart aktif degil.", 400);
        }

        if (!tasinirKart.TesisId.HasValue || tasinirKart.TesisId.Value != kaynakDepo.TesisId.Value)
        {
            throw new BaseException("Secilen tasinir kart depolar ile ayni tesise ait olmalidir.", 400);
        }

        if (!tasinirKart.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Seçilen taşınır kartın muhasebe hesap planı bağlantısı bulunmuyor.", 400);
        }

        await EnsureDepoAccessAsync(kaynakDepo.Id, kaynakDepo.TesisId, "Kaynak depo", cancellationToken);
        await EnsureDepoAccessAsync(hedefDepo.Id, hedefDepo.TesisId, "Hedef depo", cancellationToken);
        await EnsureOpenPeriodAsync(kaynakDepo.TesisId, request.HareketTarihi, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var kaynakBakiye = await _repository.GetBakiyeMiktariAsync(request.KaynakDepoId, request.TasinirKartId, cancellationToken);
            if (kaynakBakiye < request.Miktar)
            {
                throw new BaseException("Kaynak depoda transfer için yeterli stok bulunmamaktadır.", 400);
            }

            var transferGrupId = Guid.NewGuid();
            var tutar = CalculateTutar(request.Miktar, request.BirimFiyat);

            var kaynakHareket = CreateTransferLeg(
                request,
                transferGrupId,
                request.KaynakDepoId,
                request.HedefDepoId,
                StokTransferYonleri.Cikis,
                tutar);

            var hedefHareket = CreateTransferLeg(
                request,
                transferGrupId,
                request.HedefDepoId,
                request.KaynakDepoId,
                StokTransferYonleri.Giris,
                tutar);

            await _dbContext.StokHareketleri.AddRangeAsync([kaynakHareket, hedefHareket], cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return [_mapper.Map<StokHareketDto>(kaynakHareket), _mapper.Map<StokHareketDto>(hedefHareket)];
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task TransferIptalAsync(int id, CancellationToken cancellationToken = default)
    {
        var visible = await GetByIdAsync(id);
        if (visible is null)
        {
            throw new BaseException("Stok hareket bulunamadı.", 404);
        }

        var hareket = await _dbContext.StokHareketleri.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Stok hareket bulunamadı.", 404);

        if (!hareket.TransferGrupId.HasValue)
        {
            throw new BaseException("Bu stok hareketi bir transfer kaydi degildir.", 400);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var transferHareketleri = await _dbContext.StokHareketleri
                .Where(x => x.TransferGrupId == hareket.TransferGrupId.Value && x.Durum == StokHareketDurumlari.Aktif)
                .ToListAsync(cancellationToken);

            var transferGrubu = ValidateTransferIptalGrubu(transferHareketleri);
            var depoMap = await GetDepoMapAsync(transferHareketleri.Select(x => x.DepoId).Distinct(), cancellationToken);

            foreach (var item in transferHareketleri)
            {
                var depo = depoMap[item.DepoId];
                await EnsureOpenPeriodAsync(depo.TesisId, item.HareketTarihi, cancellationToken);
            }

            var hedefBakiye = await _repository.GetBakiyeMiktariAsync(transferGrubu.GirisAyagi.DepoId, transferGrubu.GirisAyagi.TasinirKartId, cancellationToken);
            if (hedefBakiye < transferGrubu.GirisAyagi.Miktar)
            {
                throw new BaseException("Hedef depodaki stok kullanıldığı için transfer iptal edilemez.", 400);
            }

            foreach (var item in transferHareketleri)
            {
                item.Durum = StokHareketDurumlari.Iptal;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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

    public async Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        if (depoId <= 0)
        {
            throw new BaseException("Gecerli bir depo secilmelidir.", 400);
        }

        if (tasinirKartId <= 0)
        {
            throw new BaseException("Gecerli bir tasinir kart secilmelidir.", 400);
        }

        var depo = await GetDepoOrThrowAsync(depoId);
        await EnsureDepoAccessAsync(depo.Id, depo.TesisId, "Depo", cancellationToken);
        var tasinirKart = await _tasinirKartRepository.GetByIdAsync(tasinirKartId)
            ?? throw new BaseException("Secilen tasinir kart bulunamadi.", 400);
        EnsureDepoVeTasinirKartAyniTesiste(depo, tasinirKart);

        return await _repository.GetStokDetayAsync(depoId, tasinirKartId, depo.MalzemeKayitTipi, cancellationToken);
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
        dto.SayimFarkiYonu = NormalizeOptional(dto.SayimFarkiYonu);

        if (string.Equals(dto.HareketTipi, StokHareketTipleri.Transfer, StringComparison.Ordinal))
        {
            throw new BaseException("Transfer hareketleri yalnizca transfer olusturma akisi ile kaydedilebilir.", 400);
        }

        if (string.Equals(dto.HareketTipi, StokHareketTipleri.SayimFarki, StringComparison.Ordinal))
        {
            if (!StokSayimFarkiYonleri.Hepsi.Contains(dto.SayimFarkiYonu ?? string.Empty))
            {
                throw new BaseException("Sayım farkı yönü Fazla veya Eksik olmalıdır.", 400);
            }
        }
        else
        {
            dto.SayimFarkiYonu = null;
        }

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

        EnsureDepoVeTasinirKartAyniTesiste(depo, tasinirKart);

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

        await EnsureOpenPeriodAsync(depo.TesisId, dto.HareketTarihi, CancellationToken.None);
    }

    private async Task EnsureOpenPeriodAsync(int? tesisId, DateTime tarih, CancellationToken cancellationToken)
    {
        await MuhasebeDonemKontrolHelper.EnsureOpenPeriodAsync(_muhasebeDonemService, tesisId, tarih, cancellationToken);
    }

    private async Task ApplyKdvAsync(StokHareketDto dto)
    {
        if (string.Equals(dto.HareketTipi, StokHareketTipleri.SayimFarki, StringComparison.Ordinal))
        {
            dto.KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi;
            dto.KdvIstisnaTanimId = null;
            dto.KdvIstisnaKodu = null;
            dto.KdvIstisnaAciklamasi = null;
            dto.KdvOrani = 0;
            dto.KdvTutari = 0;
            return;
        }

        var islemYonu = StokHareketTipleri.IsCikisEtkisi(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu)
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

    private static void NormalizeTransferRequest(StokTransferRequest request)
    {
        request.BelgeNo = NormalizeOptional(request.BelgeNo);
        request.Aciklama = NormalizeOptional(request.Aciklama);
    }

    private static void ValidateTransferDepo(STYS.Muhasebe.Depolar.Entities.Depo depo, string label)
    {
        if (!depo.AktifMi)
        {
            throw new BaseException($"{label} aktif degil.", 400);
        }

        if (!depo.TesisId.HasValue)
        {
            throw new BaseException($"{label} icin tesis bilgisi bulunamadi.", 400);
        }

        if (!depo.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException($"{label} icin muhasebe hesap plani baglantisi bulunmuyor.", 400);
        }
    }

    private async Task<STYS.Muhasebe.Depolar.Entities.Depo> GetDepoOrThrowAsync(int depoId)
    {
        return await _depoRepository.GetByIdAsync(depoId)
            ?? throw new BaseException("Depo bulunamadi.", 400);
    }

    private async Task<Dictionary<int, STYS.Muhasebe.Depolar.Entities.Depo>> GetDepoMapAsync(IEnumerable<int> depoIds, CancellationToken cancellationToken)
    {
        var ids = depoIds.Distinct().ToArray();
        var depolar = await _depoRepository.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        var depoMap = depolar.ToDictionary(x => x.Id);
        foreach (var id in ids)
        {
            if (!depoMap.ContainsKey(id))
            {
                throw new BaseException("Transfere bagli depo bulunamadi.", 400);
            }
        }

        return depoMap;
    }

    private static TransferIptalGrubu ValidateTransferIptalGrubu(List<StokHareket> transferHareketleri)
    {
        if (transferHareketleri.Count != 2)
        {
            throw new BaseException("Transfer grup butunlugu bozuk oldugu icin iptal islemi yapilamaz.", 400);
        }

        var girisAyagi = transferHareketleri.SingleOrDefault(x => string.Equals(x.TransferYonu, StokTransferYonleri.Giris, StringComparison.Ordinal));
        var cikisAyagi = transferHareketleri.SingleOrDefault(x => string.Equals(x.TransferYonu, StokTransferYonleri.Cikis, StringComparison.Ordinal));
        if (girisAyagi is null || cikisAyagi is null)
        {
            throw new BaseException("Transfer grup butunlugu bozuk oldugu icin iptal islemi yapilamaz.", 400);
        }

        if (girisAyagi.TransferGrupId != cikisAyagi.TransferGrupId
            || girisAyagi.TasinirKartId != cikisAyagi.TasinirKartId
            || girisAyagi.Miktar != cikisAyagi.Miktar
            || girisAyagi.DepoId == cikisAyagi.DepoId
            || girisAyagi.KarsiDepoId != cikisAyagi.DepoId
            || cikisAyagi.KarsiDepoId != girisAyagi.DepoId)
        {
            throw new BaseException("Transfer grup butunlugu bozuk oldugu icin iptal islemi yapilamaz.", 400);
        }

        return new TransferIptalGrubu(girisAyagi, cikisAyagi);
    }

    private async Task EnsureDepoAccessAsync(int depoId, int? tesisId, string label, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (!scope.IsScoped)
        {
            return;
        }

        if (!tesisId.HasValue || !scope.TesisIds.Contains(tesisId.Value))
        {
            throw new BaseException($"{label} icin yetkiniz bulunmuyor.", 403);
        }

        var allowedDepo = await _depoRepository.AnyAsync(x => x.Id == depoId && x.TesisId == tesisId.Value);
        if (!allowedDepo)
        {
            throw new BaseException($"{label} bulunamadi.", 400);
        }
    }

    private async Task EnsureCreateDoesNotGoNegativeAsync(StokHareketDto dto, CancellationToken cancellationToken)
    {
        var currentBalance = await GetCurrentBalanceAsync(dto.DepoId, dto.TasinirKartId, cancellationToken);
        var projectedBalance = CalculateProjectedBalance(currentBalance, 0m, CalculateMovementEffect(dto));
        EnsureNonNegativeBalance(projectedBalance, "Depoda bu işlem için yeterli stok bulunmamaktadır.");
    }

    private async Task EnsureUpdateDoesNotGoNegativeAsync(StokHareket existing, StokHareketDto dto, CancellationToken cancellationToken)
    {
        foreach (var key in GetAffectedStockKeys(existing, dto))
        {
            var currentBalance = await GetCurrentBalanceAsync(key.DepoId, key.TasinirKartId, cancellationToken);
            var existingEffect = existing.DepoId == key.DepoId && existing.TasinirKartId == key.TasinirKartId
                ? CalculateMovementEffect(existing)
                : 0m;
            var newEffect = dto.DepoId == key.DepoId && dto.TasinirKartId == key.TasinirKartId
                ? CalculateMovementEffect(dto)
                : 0m;

            var projectedBalance = CalculateProjectedBalance(currentBalance, existingEffect, newEffect);
            EnsureNonNegativeBalance(projectedBalance, "Depoda bu işlem için yeterli stok bulunmamaktadır.");
        }
    }

    private async Task EnsureDeleteDoesNotGoNegativeAsync(StokHareket existing, CancellationToken cancellationToken)
    {
        var currentBalance = await GetCurrentBalanceAsync(existing.DepoId, existing.TasinirKartId, cancellationToken);
        var projectedBalance = CalculateProjectedBalance(currentBalance, CalculateMovementEffect(existing), 0m);
        EnsureNonNegativeBalance(projectedBalance, "Bu stok hareketi silinirse depo bakiyesi negatif olacağı için işlem yapılamaz.");
    }

    private async Task<decimal> GetCurrentBalanceAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken)
        => await _repository.GetBakiyeMiktariAsync(depoId, tasinirKartId, cancellationToken);

    private async Task<StokHareket?> GetExistingMovementSnapshotAsync(int id, CancellationToken cancellationToken)
        => await _dbContext.StokHareketleri
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    private static decimal CalculateProjectedBalance(decimal currentBalance, decimal existingMovementEffect, decimal newMovementEffect)
        => currentBalance - existingMovementEffect + newMovementEffect;

    private static decimal CalculateMovementEffect(StokHareketDto dto)
        => CalculateMovementEffect(dto.HareketTipi, dto.TransferYonu, dto.SayimFarkiYonu, dto.Miktar, dto.Durum);

    private static decimal CalculateMovementEffect(StokHareket hareket)
        => CalculateMovementEffect(hareket.HareketTipi, hareket.TransferYonu, hareket.SayimFarkiYonu, hareket.Miktar, hareket.Durum);

    private static decimal CalculateMovementEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar, string? durum)
    {
        if (!string.Equals(durum, StokHareketDurumlari.Aktif, StringComparison.Ordinal))
        {
            return 0m;
        }

        if (StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return miktar;
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return -miktar;
        }

        return 0m;
    }

    private static void EnsureNonNegativeBalance(decimal projectedBalance, string errorMessage)
    {
        if (projectedBalance < 0)
        {
            throw new BaseException(errorMessage, 400);
        }
    }

    private void DetachTrackedStokHareket(int id)
    {
        var trackedEntry = _dbContext.ChangeTracker
            .Entries<StokHareket>()
            .FirstOrDefault(x => x.Entity.Id == id);

        if (trackedEntry is not null)
        {
            trackedEntry.State = EntityState.Detached;
        }
    }

    private static HashSet<StockKey> GetAffectedStockKeys(StokHareket existing, StokHareketDto dto)
        =>
        [
            new StockKey(existing.DepoId, existing.TasinirKartId),
            new StockKey(dto.DepoId, dto.TasinirKartId)
        ];

    private static void EnsureDepoVeTasinirKartAyniTesiste(STYS.Muhasebe.Depolar.Entities.Depo depo, STYS.Muhasebe.TasinirKartlari.Entities.TasinirKart tasinirKart)
    {
        if (!depo.TesisId.HasValue
            || !tasinirKart.TesisId.HasValue
            || depo.TesisId.Value != tasinirKart.TesisId.Value)
        {
            throw new BaseException("Seçilen depo ve taşınır kart aynı tesise ait olmalıdır.", 400);
        }
    }

    private static StokHareket CreateTransferLeg(
        StokTransferRequest request,
        Guid transferGrupId,
        int depoId,
        int karsiDepoId,
        string transferYonu,
        decimal tutar)
    {
        return new StokHareket
        {
            DepoId = depoId,
            TasinirKartId = request.TasinirKartId,
            HareketTarihi = request.HareketTarihi,
            HareketTipi = StokHareketTipleri.Transfer,
            Miktar = request.Miktar,
            BirimFiyat = request.BirimFiyat,
            Tutar = tutar,
            BelgeNo = request.BelgeNo,
            BelgeTarihi = request.BelgeTarihi,
            Aciklama = request.Aciklama,
            Durum = StokHareketDurumlari.Aktif,
            TransferGrupId = transferGrupId,
            TransferYonu = transferYonu,
            KarsiDepoId = karsiDepoId,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0,
            KdvIstisnaTanimId = null,
            KdvIstisnaKodu = null,
            KdvIstisnaAciklamasi = null
        };
    }

    private static decimal CalculateTutar(decimal miktar, decimal birimFiyat)
        => Math.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct StockKey(int DepoId, int TasinirKartId);

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

    private sealed record TransferIptalGrubu(StokHareket GirisAyagi, StokHareket CikisAyagi);
}
