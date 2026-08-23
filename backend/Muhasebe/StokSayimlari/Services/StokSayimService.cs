using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokSayimlari.Dtos;
using STYS.Muhasebe.StokSayimlari.Entities;
using STYS.Muhasebe.StokSayimlari.Repositories;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKartlari.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.Muhasebe.StokSayimlari.Services;

public class StokSayimService : BaseRdbmsService<StokSayimDto, StokSayim, int>, IStokSayimService
{
    private const string ConcurrencyErrorMessage = "Sayım sırasında stok hareketi oluştu. Sayım bilgilerini yenileyiniz.";

    private readonly StysAppDbContext _dbContext;
    private readonly IStokSayimRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IStokHareketService _stokHareketService;
    private readonly IMapper _mapper;

    public StokSayimService(
        StysAppDbContext dbContext,
        IStokSayimRepository repository,
        IDepoRepository depoRepository,
        ITasinirKartRepository tasinirKartRepository,
        IUserAccessScopeService userAccessScopeService,
        IStokHareketService stokHareketService,
        IMapper mapper)
        : base(repository, mapper)
    {
        _dbContext = dbContext;
        _repository = repository;
        _depoRepository = depoRepository;
        _tasinirKartRepository = tasinirKartRepository;
        _userAccessScopeService = userAccessScopeService;
        _stokHareketService = stokHareketService;
        _mapper = mapper;
    }

    public override async Task<StokSayimDto> AddAsync(StokSayimDto dto)
    {
        var depo = await ResolveAndValidateDepoAsync(dto.DepoId, dto.TesisId);
        dto.TesisId = depo.TesisId!.Value;
        dto.SayimTarihi = dto.SayimTarihi == default ? DateTime.UtcNow : dto.SayimTarihi;
        dto.Durum = StokSayimDurumlari.Taslak;
        dto.Aciklama = NormalizeOptional(dto.Aciklama);

        var entity = _mapper.Map<StokSayim>(dto);
        entity.Satirlar = await BuildSnapshotRowsAsync(depo.Id, CancellationToken.None);

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id);
    }

    public override async Task<StokSayimDto> UpdateAsync(StokSayimDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Stok sayım id zorunludur.", 400);
        }

        var entity = await GetEditableEntityAsync(dto.Id.Value, CancellationToken.None);
        entity.SayimTarihi = dto.SayimTarihi == default ? entity.SayimTarihi : dto.SayimTarihi;
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

    public override async Task<StokSayimDto?> GetByIdAsync(int id, Func<IQueryable<StokSayim>, IQueryable<StokSayim>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var entity = await BuildScopedQuery(scope)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entity is null ? null : _mapper.Map<StokSayimDto>(entity);
    }

    public override async Task<IEnumerable<StokSayimDto>> GetAllAsync(Func<IQueryable<StokSayim>, IQueryable<StokSayim>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .OrderByDescending(x => x.SayimTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<StokSayimDto>>(items);
    }

    public override async Task<IEnumerable<StokSayimDto>> WhereAsync(System.Linq.Expressions.Expression<Func<StokSayim, bool>> predicate, Func<IQueryable<StokSayim>, IQueryable<StokSayim>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .Where(predicate)
            .OrderByDescending(x => x.SayimTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<StokSayimDto>>(items);
    }

    public override async Task<PagedResult<StokSayimDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<StokSayim, bool>>? predicate = null, Func<IQueryable<StokSayim>, IQueryable<StokSayim>>? include = null, Func<IQueryable<StokSayim>, IOrderedQueryable<StokSayim>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var query = BuildScopedQuery(scope);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync();
        var ordered = orderBy is null ? query.OrderByDescending(x => x.SayimTarihi).ThenByDescending(x => x.Id) : orderBy(query);
        var items = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<StokSayimDto>(_mapper.Map<List<StokSayimDto>>(items), request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<StokSayimDto> UpdateSatirlarAsync(int id, UpdateStokSayimSatirlarRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var incomingMap = request.Satirlar.ToDictionary(x => x.Id);

        foreach (var satir in entity.Satirlar)
        {
            if (!incomingMap.TryGetValue(satir.Id, out var incoming))
            {
                continue;
            }

            ValidateSayilanMiktar(satir.TakipTipi, incoming.SayilanMiktar);
            satir.SayilanMiktar = incoming.SayilanMiktar;
            satir.FarkMiktari = incoming.SayilanMiktar - satir.SistemMiktari;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StokSayimDto> AddSatirAsync(int id, AddStokSayimSatirRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var kart = await _tasinirKartRepository.GetByIdAsync(request.TasinirKartId)
            ?? throw new BaseException("Seçilen taşınır kart bulunamadı.", 400);

        if (!kart.TesisId.HasValue || kart.TesisId.Value != entity.TesisId)
        {
            throw new BaseException("Seçilen taşınır kart sayım deposu ile aynı tesise ait olmalıdır.", 400);
        }

        var takipTipi = ResolveTakipTipi(kart);
        var row = await BuildManualRowAsync(entity, kart, takipTipi, request, cancellationToken);

        if (entity.Satirlar.Any(x => SameSnapshotKey(x, row)))
        {
            throw new BaseException("Aynı Taşınır Kart / Lot / Seri için sayım satırı zaten mevcut.", 400);
        }

        entity.Satirlar.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var satir = entity.Satirlar.FirstOrDefault(x => x.Id == satirId)
            ?? throw new BaseException("Sayım satırı bulunamadı.", 404);

        if (satir.SistemMiktari != 0)
        {
            throw new BaseException("Sistem snapshot satırları silinemez. Sayılan miktarı güncelleyiniz.", 400);
        }

        satir.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<StokSayimDto> RefreshAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var freshRows = await BuildSnapshotRowsAsync(entity.DepoId, cancellationToken);

        _dbContext.StokSayimSatirlari.RemoveRange(entity.Satirlar);
        entity.Satirlar = freshRows;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<StokSayimDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var entity = await GetEditableEntityAsync(id, cancellationToken);
            var liveSnapshot = await BuildCurrentSnapshotMapAsync(entity.DepoId, cancellationToken);
            EnsureSnapshotMatches(entity.Satirlar.Where(x => !x.IsDeleted).ToList(), liveSnapshot);

            foreach (var satir in entity.Satirlar.Where(x => !x.IsDeleted))
            {
                satir.FarkMiktari = satir.SayilanMiktar - satir.SistemMiktari;
                if (satir.FarkMiktari == 0)
                {
                    continue;
                }

                await _stokHareketService.AddWithinCurrentTransactionAsync(BuildSayimFarkiDto(entity, satir), cancellationToken);
            }

            entity.Durum = StokSayimDurumlari.Kesinlesti;
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

    public async Task<StokSayimDto> IptalAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        entity.Durum = StokSayimDurumlari.Iptal;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    private async Task<STYS.Muhasebe.Depolar.Entities.Depo> ResolveAndValidateDepoAsync(int depoId, int? tesisId)
    {
        var depo = await _depoRepository.GetByIdAsync(depoId)
            ?? throw new BaseException("Seçilen depo bulunamadı.", 400);

        if (!depo.TesisId.HasValue)
        {
            throw new BaseException("Seçilen depo tesis bağlantısına sahip değil.", 400);
        }

        if (tesisId.HasValue && tesisId.Value > 0 && depo.TesisId.Value != tesisId.Value)
        {
            throw new BaseException("Seçilen depo ile tesis bilgisi uyumsuz.", 400);
        }

        await EnsureDepoAccessAsync(depo.TesisId.Value);
        return depo;
    }

    private async Task EnsureDepoAccessAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu depo için yetkiniz bulunmuyor.", 403);
        }
    }

    private IQueryable<StokSayim> BuildScopedQuery(DomainAccessScope scope)
    {
        var query = _dbContext.StokSayimlar
            .AsNoTracking()
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        return query;
    }

    private async Task<StokSayim> GetEditableEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.StokSayimlar
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Stok sayımı bulunamadı.", 404);

        await EnsureDepoAccessAsync(entity.TesisId);

        if (!string.Equals(entity.Durum, StokSayimDurumlari.Taslak, StringComparison.Ordinal))
        {
            throw new BaseException("Sadece taslak stok sayımları değiştirilebilir.", 400);
        }

        return entity;
    }

    private async Task<StokSayimDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken = default)
        => await GetByIdAsync(id)
           ?? throw new BaseException("Stok sayımı bulunamadı.", 404);

    private async Task<List<StokSayimSatir>> BuildSnapshotRowsAsync(int depoId, CancellationToken cancellationToken)
    {
        var items = await BuildCurrentSnapshotRowsAsync(depoId, cancellationToken);
        return items
            .Select(x => new StokSayimSatir
            {
                TasinirKartId = x.TasinirKartId,
                StokLotId = x.StokLotId,
                StokSeriId = x.StokSeriId,
                TakipTipi = x.TakipTipi,
                StokKodu = x.StokKodu,
                TasinirKartAd = x.TasinirKartAd,
                Birim = x.Birim,
                LotNo = x.LotNo,
                SonKullanmaTarihi = x.SonKullanmaTarihi,
                SeriNo = x.SeriNo,
                SistemMiktari = x.SistemMiktari,
                SayilanMiktar = x.SistemMiktari,
                FarkMiktari = 0
            })
            .ToList();
    }

    private async Task<List<SnapshotRow>> BuildCurrentSnapshotRowsAsync(int depoId, CancellationToken cancellationToken)
    {
        var hareketler = await _dbContext.StokHareketleri
            .AsNoTracking()
            .Include(x => x.TasinirKart)
            .Include(x => x.StokLot)
            .Include(x => x.StokSeri)
            .Where(x => x.DepoId == depoId && x.Durum == StokHareketDurumlari.Aktif)
            .Select(x => new SnapshotMovementRow
            {
                TasinirKartId = x.TasinirKartId,
                TakipTipi = x.TasinirKart != null ? x.TasinirKart.TakipTipi : TasinirKartTakipTipleri.Yok,
                TakipliMi = x.TasinirKart != null && x.TasinirKart.TakipliMi,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : "Adet",
                StokLotId = x.StokLotId,
                StokSeriId = x.StokSeriId,
                LotNo = x.StokLot != null ? x.StokLot.LotNo : null,
                SonKullanmaTarihi = x.StokLot != null ? x.StokLot.SonKullanmaTarihi : null,
                SeriNo = x.StokSeri != null ? x.StokSeri.SeriNo : null,
                Effect = CalculateMovementEffect(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar)
            })
            .ToListAsync(cancellationToken);

        return hareketler
            .GroupBy(BuildSnapshotGroupKey)
            .Select(g => new SnapshotRow
            {
                TasinirKartId = g.Key.TasinirKartId,
                StokLotId = g.Key.StokLotId,
                StokSeriId = g.Key.StokSeriId,
                TakipTipi = g.Key.TakipTipi,
                StokKodu = g.First().StokKodu,
                TasinirKartAd = g.First().TasinirKartAd,
                Birim = g.First().Birim,
                LotNo = g.First().LotNo,
                SonKullanmaTarihi = g.First().SonKullanmaTarihi,
                SeriNo = g.First().SeriNo,
                SistemMiktari = g.Sum(x => x.Effect)
            })
            .Where(x => x.SistemMiktari > 0)
            .OrderBy(x => x.StokKodu)
            .ThenBy(x => x.LotNo)
            .ThenBy(x => x.SeriNo)
            .ToList();
    }

    private async Task<Dictionary<SnapshotKey, decimal>> BuildCurrentSnapshotMapAsync(int depoId, CancellationToken cancellationToken)
    {
        var rows = await BuildCurrentSnapshotRowsAsync(depoId, cancellationToken);
        return rows.ToDictionary(
            CreateSnapshotKey,
            x => x.SistemMiktari);
    }

    private void EnsureSnapshotMatches(List<StokSayimSatir> satirlar, Dictionary<SnapshotKey, decimal> liveSnapshot)
    {
        var expected = satirlar.ToDictionary(
            CreateSnapshotKey,
            x => x.SistemMiktari);

        var keys = expected.Keys.Union(liveSnapshot.Keys).ToList();
        foreach (var key in keys)
        {
            var expectedValue = expected.TryGetValue(key, out var existing) ? existing : 0m;
            var liveValue = liveSnapshot.TryGetValue(key, out var current) ? current : 0m;
            if (expectedValue != liveValue)
            {
                throw new BaseException(ConcurrencyErrorMessage, 409);
            }
        }
    }

    private async Task<StokSayimSatir> BuildManualRowAsync(StokSayim sayim, TasinirKart kart, string takipTipi, AddStokSayimSatirRequest request, CancellationToken cancellationToken)
    {
        ValidateSayilanMiktar(takipTipi, request.SayilanMiktar);

        var row = new StokSayimSatir
        {
            TasinirKartId = kart.Id,
            TakipTipi = takipTipi,
            StokKodu = kart.StokKodu,
            TasinirKartAd = kart.Ad,
            Birim = kart.Birim,
            SistemMiktari = 0,
            SayilanMiktar = request.SayilanMiktar,
            FarkMiktari = request.SayilanMiktar
        };

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Lot, StringComparison.Ordinal))
        {
            if (request.StokLotId.HasValue)
            {
                var lot = await _dbContext.StokLotlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.StokLotId.Value, cancellationToken)
                    ?? throw new BaseException("Seçilen lot bulunamadı.", 400);
                row.StokLotId = lot.Id;
                row.LotNo = lot.LotNo;
                row.SonKullanmaTarihi = lot.SonKullanmaTarihi;
            }
            else
            {
                row.LotNo = NormalizeOptional(request.LotNo) ?? throw new BaseException("Lot no zorunludur.", 400);
                row.SonKullanmaTarihi = request.SonKullanmaTarihi;
            }
        }
        else if (string.Equals(takipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal))
        {
            if (request.StokSeriId.HasValue)
            {
                var seri = await _dbContext.StokSeriler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.StokSeriId.Value, cancellationToken)
                    ?? throw new BaseException("Seçilen seri bulunamadı.", 400);
                row.StokSeriId = seri.Id;
                row.SeriNo = seri.SeriNo;
            }
            else
            {
                row.SeriNo = NormalizeOptional(request.SeriNo) ?? throw new BaseException("Seri No zorunludur.", 400);
            }
        }

        row.StokSayimId = sayim.Id;
        return row;
    }

    private static void ValidateSayilanMiktar(string takipTipi, decimal sayilanMiktar)
    {
        if (sayilanMiktar < 0)
        {
            throw new BaseException("Sayılan miktar negatif olamaz.", 400);
        }

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal)
            && sayilanMiktar != 0
            && sayilanMiktar != 1)
        {
            throw new BaseException("Seri takipli sayım satırlarında sayılan miktar yalnızca 0 veya 1 olabilir.", 400);
        }
    }

    private static bool SameSnapshotKey(StokSayimSatir left, StokSayimSatir right)
        => CreateSnapshotKey(left) == CreateSnapshotKey(right);

    private static string ResolveTakipTipi(TasinirKart kart)
        => TasinirKartServiceHelpers.ResolveTakipTipi(kart.TakipTipi, kart.TakipliMi);

    private static SnapshotGroupKey BuildSnapshotGroupKey(SnapshotMovementRow row)
    {
        var takipTipi = TasinirKartServiceHelpers.ResolveTakipTipi(row.TakipTipi, row.TakipliMi);
        return takipTipi switch
        {
            TasinirKartTakipTipleri.Lot => new SnapshotGroupKey(row.TasinirKartId, row.StokLotId, null, NormalizeIdentityPart(row.LotNo), null, takipTipi),
            TasinirKartTakipTipleri.Seri => new SnapshotGroupKey(row.TasinirKartId, null, row.StokSeriId, null, NormalizeIdentityPart(row.SeriNo), takipTipi),
            _ => new SnapshotGroupKey(row.TasinirKartId, null, null, null, null, TasinirKartTakipTipleri.Yok)
        };
    }

    private static SnapshotKey CreateSnapshotKey(SnapshotRow row)
        => new(row.TasinirKartId, row.StokLotId, row.StokSeriId, NormalizeIdentityPart(row.LotNo), NormalizeIdentityPart(row.SeriNo));

    private static SnapshotKey CreateSnapshotKey(StokSayimSatir row)
        => new(row.TasinirKartId, row.StokLotId, row.StokSeriId, NormalizeIdentityPart(row.LotNo), NormalizeIdentityPart(row.SeriNo));

    private static string? NormalizeIdentityPart(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static decimal CalculateMovementEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar)
    {
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

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StokHareketDto BuildSayimFarkiDto(StokSayim sayim, StokSayimSatir satir)
    {
        var fark = satir.SayilanMiktar - satir.SistemMiktari;
        var yon = fark > 0 ? StokSayimFarkiYonleri.Fazla : StokSayimFarkiYonleri.Eksik;

        return new StokHareketDto
        {
            DepoId = sayim.DepoId,
            TasinirKartId = satir.TasinirKartId,
            HareketTarihi = sayim.SayimTarihi,
            HareketTipi = StokHareketTipleri.SayimFarki,
            SayimFarkiYonu = yon,
            Miktar = Math.Abs(fark),
            BirimFiyat = 0,
            Tutar = 0,
            Aciklama = sayim.Aciklama,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = 4,
            KdvOrani = 0,
            StokLotId = satir.StokLotId,
            StokSeriId = satir.StokSeriId,
            LotNo = satir.LotNo,
            SonKullanmaTarihi = satir.SonKullanmaTarihi,
            SeriNo = satir.SeriNo
        };
    }

    private sealed record SnapshotMovementRow
    {
        public int TasinirKartId { get; init; }
        public string? TakipTipi { get; init; }
        public bool TakipliMi { get; init; }
        public string StokKodu { get; init; } = string.Empty;
        public string TasinirKartAd { get; init; } = string.Empty;
        public string Birim { get; init; } = "Adet";
        public int? StokLotId { get; init; }
        public int? StokSeriId { get; init; }
        public string? LotNo { get; init; }
        public DateTime? SonKullanmaTarihi { get; init; }
        public string? SeriNo { get; init; }
        public decimal Effect { get; init; }
    }

    private sealed record SnapshotRow
    {
        public int TasinirKartId { get; init; }
        public int? StokLotId { get; init; }
        public int? StokSeriId { get; init; }
        public string TakipTipi { get; init; } = string.Empty;
        public string StokKodu { get; init; } = string.Empty;
        public string TasinirKartAd { get; init; } = string.Empty;
        public string Birim { get; init; } = string.Empty;
        public string? LotNo { get; init; }
        public DateTime? SonKullanmaTarihi { get; init; }
        public string? SeriNo { get; init; }
        public decimal SistemMiktari { get; init; }
    }

    private readonly record struct SnapshotGroupKey(int TasinirKartId, int? StokLotId, int? StokSeriId, string? LotNoIdentity, string? SeriNoIdentity, string TakipTipi);
    private readonly record struct SnapshotKey(int TasinirKartId, int? StokLotId, int? StokSeriId, string? LotNoIdentity, string? SeriNoIdentity);
}
