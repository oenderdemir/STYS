using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Entities;
using STYS.Muhasebe.StokTalepleri.Repositories;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.StokTalepleri.Services;

public class StokTalepService : BaseRdbmsService<StokTalepDto, StokTalep, int>, IStokTalepService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IStokTalepRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStokHareketService _stokHareketService;
    private readonly IMapper _mapper;

    public StokTalepService(
        StysAppDbContext dbContext,
        IStokTalepRepository repository,
        IDepoRepository depoRepository,
        ITasinirKartRepository tasinirKartRepository,
        IUserAccessScopeService userAccessScopeService,
        ICurrentUserAccessor currentUserAccessor,
        IStokHareketService stokHareketService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _dbContext = dbContext;
        _repository = repository;
        _depoRepository = depoRepository;
        _tasinirKartRepository = tasinirKartRepository;
        _userAccessScopeService = userAccessScopeService;
        _currentUserAccessor = currentUserAccessor;
        _stokHareketService = stokHareketService;
        _mapper = mapper;
    }

    public override async Task<StokTalepDto> AddAsync(StokTalepDto dto)
    {
        var depolar = await ValidateDepolarAsync(dto.TalepEdenDepoId, dto.KarsilayanDepoId);
        dto.TesisId = depolar.TesisId;
        dto.TalepTarihi = dto.TalepTarihi == default ? DateTime.UtcNow : dto.TalepTarihi;
        dto.Durum = StokTalepDurumlari.Taslak;
        dto.Aciklama = NormalizeOptional(dto.Aciklama);
        dto.TalepEdenKullaniciId = _currentUserAccessor.GetCurrentUserId();
        dto.Satirlar = [];

        var entity = _mapper.Map<StokTalep>(dto);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id);
    }

    public override async Task<StokTalepDto> UpdateAsync(StokTalepDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Stok talep id zorunludur.", 400);
        }

        var entity = await GetEditableEntityAsync(dto.Id.Value, CancellationToken.None);
        var depolar = await ValidateDepolarAsync(dto.TalepEdenDepoId, dto.KarsilayanDepoId);
        entity.TesisId = depolar.TesisId;
        entity.TalepEdenDepoId = dto.TalepEdenDepoId;
        entity.KarsilayanDepoId = dto.KarsilayanDepoId;
        entity.TalepTarihi = dto.TalepTarihi == default ? entity.TalepTarihi : dto.TalepTarihi;
        entity.Aciklama = NormalizeOptional(dto.Aciklama);
        await _dbContext.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await GetEditableEntityAsync(id, CancellationToken.None);
        entity.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
    }

    public override async Task<StokTalepDto?> GetByIdAsync(int id, Func<IQueryable<StokTalep>, IQueryable<StokTalep>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var entity = await BuildScopedQuery(scope)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : _mapper.Map<StokTalepDto>(entity);
    }

    public override async Task<IEnumerable<StokTalepDto>> GetAllAsync(Func<IQueryable<StokTalep>, IQueryable<StokTalep>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .OrderByDescending(x => x.TalepTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<StokTalepDto>>(items);
    }

    public override async Task<IEnumerable<StokTalepDto>> WhereAsync(System.Linq.Expressions.Expression<Func<StokTalep, bool>> predicate, Func<IQueryable<StokTalep>, IQueryable<StokTalep>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .Where(predicate)
            .OrderByDescending(x => x.TalepTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<StokTalepDto>>(items);
    }

    public override async Task<PagedResult<StokTalepDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<StokTalep, bool>>? predicate = null, Func<IQueryable<StokTalep>, IQueryable<StokTalep>>? include = null, Func<IQueryable<StokTalep>, IOrderedQueryable<StokTalep>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var query = BuildScopedQuery(scope);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync();
        var ordered = orderBy is null ? query.OrderByDescending(x => x.TalepTarihi).ThenByDescending(x => x.Id) : orderBy(query);
        var items = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<StokTalepDto>(_mapper.Map<List<StokTalepDto>>(items), request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<StokTalepDto> UpdateSatirlarAsync(int id, UpdateStokTalepSatirlarRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetSatirGuncellenebilirEntityAsync(id, cancellationToken);
        var incomingMap = request.Satirlar.ToDictionary(x => x.Id);

        foreach (var satir in entity.Satirlar)
        {
            if (!incomingMap.TryGetValue(satir.Id, out var incoming))
            {
                continue;
            }

            ValidateRequestedQuantity(incoming.TalepMiktari);
            ValidateApprovedQuantity(incoming.OnaylananMiktar, incoming.TalepMiktari);
            ValidateTrackedQuantity(satir.TakipTipi, incoming.TalepMiktari, incoming.OnaylananMiktar);

            satir.TalepMiktari = incoming.TalepMiktari;
            satir.OnaylananMiktar = incoming.OnaylananMiktar;
            satir.Aciklama = NormalizeOptional(incoming.Aciklama);
        }

        entity.Durum = DetermineApprovalStatus(entity.Satirlar.Where(x => !x.IsDeleted).ToList(), entity.Durum);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StokTalepDto> AddSatirAsync(int id, AddStokTalepSatirRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var kart = await _tasinirKartRepository.GetByIdAsync(request.TasinirKartId)
            ?? throw new BaseException("Secilen tasinir kart bulunamadi.", 400);

        if (!kart.TesisId.HasValue || kart.TesisId.Value != entity.TesisId)
        {
            throw new BaseException("Secilen tasinir kart talep ile ayni tesise ait olmalidir.", 400);
        }

        ValidateRequestedQuantity(request.TalepMiktari);
        var takipTipi = ResolveTakipTipi(kart);
        ValidateTrackedQuantity(takipTipi, request.TalepMiktari, 0);

        entity.Satirlar.Add(new StokTalepSatir
        {
            TasinirKartId = kart.Id,
            TakipTipi = takipTipi,
            StokKodu = kart.StokKodu ?? string.Empty,
            TasinirKartAd = kart.Ad,
            Birim = kart.Birim,
            TalepMiktari = request.TalepMiktari,
            OnaylananMiktar = 0,
            TeslimEdilenMiktar = 0,
            Aciklama = NormalizeOptional(request.Aciklama)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var satir = entity.Satirlar.FirstOrDefault(x => x.Id == satirId)
            ?? throw new BaseException("Stok talep satiri bulunamadi.", 404);

        satir.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StokTalepDto> GonderAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        if (!entity.Satirlar.Any(x => !x.IsDeleted))
        {
            throw new BaseException("Stok talebi gonderilmeden once en az bir satir eklenmelidir.", 400);
        }

        entity.Durum = StokTalepDurumlari.Bekliyor;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StokTalepDto> ReddetAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetAwaitingOrApprovedEntityAsync(id, cancellationToken);
        foreach (var satir in entity.Satirlar.Where(x => !x.IsDeleted))
        {
            satir.OnaylananMiktar = 0;
        }

        entity.Durum = StokTalepDurumlari.Reddedildi;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StokTalepDto> TeslimEtAsync(int id, TeslimEtStokTalepRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var entity = await _dbContext.StokTalepler
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Stok talebi bulunamadi.", 404);

            await EnsureDepoAccessAsync(entity.TesisId);

            if (!string.Equals(entity.Durum, StokTalepDurumlari.Onaylandi, StringComparison.Ordinal)
                && !string.Equals(entity.Durum, StokTalepDurumlari.KismiOnaylandi, StringComparison.Ordinal))
            {
                throw new BaseException("Sadece onaylanmis stok talepleri teslim edilebilir.", 400);
            }

            if (entity.Satirlar.Any(x => x.TeslimEdilenMiktar > 0))
            {
                throw new BaseException("Bu stok talebi daha once teslim edildigi icin tekrar teslim edilemez.", 400);
            }

            var teslimMap = request.Satirlar.ToDictionary(x => x.Id);
            foreach (var satir in entity.Satirlar.Where(x => !x.IsDeleted && x.OnaylananMiktar > 0))
            {
                teslimMap.TryGetValue(satir.Id, out var satirTeslimBilgisi);
                ValidateTrackedDeliverySelection(satir, satirTeslimBilgisi);

                var transfer = await _stokHareketService.CreateTransferAsync(new StokTransferRequest
                {
                    KaynakDepoId = entity.KarsilayanDepoId,
                    HedefDepoId = entity.TalepEdenDepoId,
                    TasinirKartId = satir.TasinirKartId,
                    HareketTarihi = entity.TalepTarihi,
                    Miktar = satir.OnaylananMiktar,
                    BirimFiyat = 0,
                    BelgeTarihi = entity.TalepTarihi,
                    Aciklama = BuildTransferDescription(entity, satir),
                    KaynakModul = "StokTalepSatir",
                    KaynakId = satir.Id,
                    StokLotId = satirTeslimBilgisi?.StokLotId,
                    StokSeriId = satirTeslimBilgisi?.StokSeriId
                }, cancellationToken);

                satir.TeslimEdilenMiktar = satir.OnaylananMiktar;
                satir.TransferGrupId = transfer.FirstOrDefault()?.TransferGrupId;
                satir.StokLotId = satirTeslimBilgisi?.StokLotId;
                satir.StokSeriId = satirTeslimBilgisi?.StokSeriId;
            }

            entity.Durum = StokTalepDurumlari.TeslimEdildi;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetRequiredDtoAsync(entity.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<StokTalepDto> IptalAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.StokTalepler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Stok talebi bulunamadi.", 404);

        await EnsureDepoAccessAsync(entity.TesisId);

        if (string.Equals(entity.Durum, StokTalepDurumlari.TeslimEdildi, StringComparison.Ordinal))
        {
            throw new BaseException("Teslim edilmis stok talebi iptal edilemez.", 400);
        }

        entity.Durum = StokTalepDurumlari.Iptal;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    private IQueryable<StokTalep> BuildScopedQuery(DomainAccessScope scope)
    {
        var query = _dbContext.StokTalepler
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        return query;
    }

    private async Task<StokTalep> GetEditableEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.StokTalepler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Stok talebi bulunamadi.", 404);

        await EnsureDepoAccessAsync(entity.TesisId);

        if (!string.Equals(entity.Durum, StokTalepDurumlari.Taslak, StringComparison.Ordinal))
        {
            throw new BaseException("Sadece taslak stok talepleri degistirilebilir.", 400);
        }

        return entity;
    }

    private async Task<StokTalep> GetSatirGuncellenebilirEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.StokTalepler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Stok talebi bulunamadi.", 404);

        await EnsureDepoAccessAsync(entity.TesisId);

        if (!string.Equals(entity.Durum, StokTalepDurumlari.Taslak, StringComparison.Ordinal)
            && !string.Equals(entity.Durum, StokTalepDurumlari.Bekliyor, StringComparison.Ordinal)
            && !string.Equals(entity.Durum, StokTalepDurumlari.Onaylandi, StringComparison.Ordinal)
            && !string.Equals(entity.Durum, StokTalepDurumlari.KismiOnaylandi, StringComparison.Ordinal))
        {
            throw new BaseException("Bu durumdaki stok talebinin satirlari guncellenemez.", 400);
        }

        return entity;
    }

    private async Task<StokTalep> GetAwaitingOrApprovedEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.StokTalepler
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Stok talebi bulunamadi.", 404);

        await EnsureDepoAccessAsync(entity.TesisId);

        if (!string.Equals(entity.Durum, StokTalepDurumlari.Bekliyor, StringComparison.Ordinal)
            && !string.Equals(entity.Durum, StokTalepDurumlari.Onaylandi, StringComparison.Ordinal)
            && !string.Equals(entity.Durum, StokTalepDurumlari.KismiOnaylandi, StringComparison.Ordinal))
        {
            throw new BaseException("Bu durumdaki stok talebi icin islem yapilamaz.", 400);
        }

        return entity;
    }

    private async Task<StokTalepDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken = default)
        => await GetByIdAsync(id)
           ?? throw new BaseException("Stok talebi bulunamadi.", 404);

    private async Task EnsureDepoAccessAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu stok talebi icin yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task<(int TesisId, int TalepEdenDepoId, int KarsilayanDepoId)> ValidateDepolarAsync(int talepEdenDepoId, int karsilayanDepoId)
    {
        if (talepEdenDepoId <= 0 || karsilayanDepoId <= 0)
        {
            throw new BaseException("Talep eden depo ve karsilayan depo secimi zorunludur.", 400);
        }

        if (talepEdenDepoId == karsilayanDepoId)
        {
            throw new BaseException("Talep eden depo ile karsilayan depo ayni olamaz.", 400);
        }

        var talepEdenDepo = await _depoRepository.GetByIdAsync(talepEdenDepoId)
            ?? throw new BaseException("Talep eden depo bulunamadi.", 400);
        var karsilayanDepo = await _depoRepository.GetByIdAsync(karsilayanDepoId)
            ?? throw new BaseException("Karsilayan depo bulunamadi.", 400);

        if (!talepEdenDepo.TesisId.HasValue || !karsilayanDepo.TesisId.HasValue || talepEdenDepo.TesisId.Value != karsilayanDepo.TesisId.Value)
        {
            throw new BaseException("Depolar ayni tesise ait olmalidir.", 400);
        }

        await EnsureDepoAccessAsync(talepEdenDepo.TesisId.Value);
        return (talepEdenDepo.TesisId.Value, talepEdenDepoId, karsilayanDepoId);
    }

    private static string ResolveTakipTipi(TasinirKart kart)
        => !string.IsNullOrWhiteSpace(kart.TakipTipi)
            ? kart.TakipTipi
            : kart.TakipliMi
                ? TasinirKartTakipTipleri.Lot
                : TasinirKartTakipTipleri.Yok;

    private static void ValidateRequestedQuantity(decimal talepMiktari)
    {
        if (talepMiktari <= 0)
        {
            throw new BaseException("Talep miktari 0'dan buyuk olmalidir.", 400);
        }
    }

    private static void ValidateApprovedQuantity(decimal onaylananMiktar, decimal talepMiktari)
    {
        if (onaylananMiktar < 0 || onaylananMiktar > talepMiktari)
        {
            throw new BaseException("Onaylanan miktar 0 ile talep miktari arasinda olmalidir.", 400);
        }
    }

    private static void ValidateTrackedQuantity(string takipTipi, decimal talepMiktari, decimal onaylananMiktar)
    {
        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal))
        {
            if (talepMiktari != 1)
            {
                throw new BaseException("Seri takipli tasinir kartlarda talep miktari 1 olmalidir.", 400);
            }

            if (onaylananMiktar != 0 && onaylananMiktar != 1)
            {
                throw new BaseException("Seri takipli tasinir kartlarda onaylanan miktar 0 veya 1 olmalidir.", 400);
            }
        }
    }

    private static void ValidateTrackedDeliverySelection(StokTalepSatir satir, TeslimEtStokTalepSatirRequest? teslim)
    {
        if (string.Equals(satir.TakipTipi, TasinirKartTakipTipleri.Lot, StringComparison.Ordinal))
        {
            if (teslim?.StokLotId is null or <= 0)
            {
                throw new BaseException("Lot takipli talep satirlarinda teslim sirasinda lot secimi zorunludur.", 400);
            }
        }

        if (string.Equals(satir.TakipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal))
        {
            if (teslim?.StokSeriId is null or <= 0)
            {
                throw new BaseException("Seri takipli talep satirlarinda teslim sirasinda seri secimi zorunludur.", 400);
            }
        }
    }

    private static string DetermineApprovalStatus(IReadOnlyCollection<StokTalepSatir> satirlar, string currentStatus)
    {
        if (string.Equals(currentStatus, StokTalepDurumlari.Taslak, StringComparison.Ordinal))
        {
            return currentStatus;
        }

        if (satirlar.Count == 0 || satirlar.All(x => x.OnaylananMiktar == 0))
        {
            return StokTalepDurumlari.Reddedildi;
        }

        if (satirlar.All(x => x.OnaylananMiktar == x.TalepMiktari))
        {
            return StokTalepDurumlari.Onaylandi;
        }

        return StokTalepDurumlari.KismiOnaylandi;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildTransferDescription(StokTalep talep, StokTalepSatir satir)
        => $"Stok Talebi #{talep.Id} - {satir.StokKodu} {satir.TasinirKartAd}";
}
