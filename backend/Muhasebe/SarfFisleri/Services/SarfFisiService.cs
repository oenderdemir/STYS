using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SarfFisleri.Dtos;
using STYS.Muhasebe.SarfFisleri.Entities;
using STYS.Muhasebe.SarfFisleri.Repositories;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Repositories;
using STYS.Muhasebe.TasinirKartlari.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.Muhasebe.SarfFisleri.Services;

public class SarfFisiService : BaseRdbmsService<SarfFisiDto, SarfFisi, int>, ISarfFisiService
{
    private readonly StysAppDbContext _dbContext;
    private readonly ISarfFisiRepository _repository;
    private readonly IDepoRepository _depoRepository;
    private readonly ITasinirKartRepository _tasinirKartRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IStokHareketService _stokHareketService;
    private readonly IMapper _mapper;

    public SarfFisiService(
        StysAppDbContext dbContext,
        ISarfFisiRepository repository,
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

    public override async Task<SarfFisiDto> AddAsync(SarfFisiDto dto)
    {
        var depo = await ResolveAndValidateDepoAsync(dto.DepoId);
        await ValidateIsletmeAlaniAsync(dto.IsletmeAlaniId, depo.TesisId!.Value, CancellationToken.None);

        dto.TesisId = depo.TesisId!.Value;
        dto.SarfTarihi = dto.SarfTarihi == default ? DateTime.UtcNow : dto.SarfTarihi;
        dto.Durum = SarfFisiDurumlari.Taslak;
        dto.OlusturanKullaniciId = _currentUserAccessor.GetCurrentUserId();
        dto.Aciklama = NormalizeOptional(dto.Aciklama);

        var entity = _mapper.Map<SarfFisi>(dto);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id, CancellationToken.None);
    }

    public override async Task<SarfFisiDto> UpdateAsync(SarfFisiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Sarf fişi id zorunludur.", 400);
        }

        var entity = await GetEditableEntityAsync(dto.Id.Value, CancellationToken.None);
        var depo = await ResolveAndValidateDepoAsync(dto.DepoId);
        await ValidateIsletmeAlaniAsync(dto.IsletmeAlaniId, depo.TesisId!.Value, CancellationToken.None);

        entity.DepoId = depo.Id;
        entity.TesisId = depo.TesisId!.Value;
        entity.SarfTarihi = dto.SarfTarihi == default ? entity.SarfTarihi : dto.SarfTarihi;
        entity.IsletmeAlaniId = dto.IsletmeAlaniId;
        entity.Aciklama = NormalizeOptional(dto.Aciklama);
        await _dbContext.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id, CancellationToken.None);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await GetEditableEntityAsync(id, CancellationToken.None);
        entity.IsDeleted = true;
        await _dbContext.SaveChangesAsync();
    }

    public override async Task<SarfFisiDto?> GetByIdAsync(int id, Func<IQueryable<SarfFisi>, IQueryable<SarfFisi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var entity = await BuildScopedQuery(scope).FirstOrDefaultAsync(x => x.Id == id);
        return entity is null ? null : _mapper.Map<SarfFisiDto>(entity);
    }

    public override async Task<IEnumerable<SarfFisiDto>> GetAllAsync(Func<IQueryable<SarfFisi>, IQueryable<SarfFisi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .OrderByDescending(x => x.SarfTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<SarfFisiDto>>(items);
    }

    public override async Task<IEnumerable<SarfFisiDto>> WhereAsync(System.Linq.Expressions.Expression<Func<SarfFisi, bool>> predicate, Func<IQueryable<SarfFisi>, IQueryable<SarfFisi>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .Where(predicate)
            .OrderByDescending(x => x.SarfTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return _mapper.Map<List<SarfFisiDto>>(items);
    }

    public override async Task<PagedResult<SarfFisiDto>> GetPagedAsync(PagedRequest request, System.Linq.Expressions.Expression<Func<SarfFisi, bool>>? predicate = null, Func<IQueryable<SarfFisi>, IQueryable<SarfFisi>>? include = null, Func<IQueryable<SarfFisi>, IOrderedQueryable<SarfFisi>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var query = BuildScopedQuery(scope);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync();
        var ordered = orderBy is null ? query.OrderByDescending(x => x.SarfTarihi).ThenByDescending(x => x.Id) : orderBy(query);
        var items = await ordered
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResult<SarfFisiDto>(_mapper.Map<List<SarfFisiDto>>(items), request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<SarfFisiDto> UpdateSatirlarAsync(int id, UpdateSarfFisiSatirlarRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var map = request.Satirlar.ToDictionary(x => x.Id);

        foreach (var satir in entity.Satirlar)
        {
            if (!map.TryGetValue(satir.Id, out var incoming))
            {
                continue;
            }

            ValidateSatirInput(satir.TakipTipi, incoming.Miktar, incoming.StokLotId, incoming.StokSeriId);
            satir.Miktar = incoming.Miktar;
            satir.StokLotId = incoming.StokLotId;
            satir.StokSeriId = incoming.StokSeriId;
            satir.Aciklama = NormalizeOptional(incoming.Aciklama);
        }

        await RefreshTrackingMetadataAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<SarfFisiDto> AddSatirAsync(int id, AddSarfFisiSatirRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var kart = await _tasinirKartRepository.GetByIdAsync(request.TasinirKartId)
            ?? throw new BaseException("Seçilen taşınır kart bulunamadı.", 400);

        if (!kart.TesisId.HasValue || kart.TesisId.Value != entity.TesisId)
        {
            throw new BaseException("Seçilen taşınır kart sarf fişi ile aynı tesise ait olmalıdır.", 400);
        }

        var takipTipi = ResolveTakipTipi(kart);
        ValidateSatirInput(takipTipi, request.Miktar, request.StokLotId, request.StokSeriId);

        var satir = new SarfFisiSatir
        {
            SarfFisiId = entity.Id,
            TasinirKartId = kart.Id,
            TakipTipi = takipTipi,
            StokKodu = kart.StokKodu,
            TasinirKartAd = kart.Ad,
            Birim = kart.Birim,
            Miktar = request.Miktar,
            StokLotId = request.StokLotId,
            StokSeriId = request.StokSeriId,
            Aciklama = NormalizeOptional(request.Aciklama)
        };

        entity.Satirlar.Add(satir);
        await RefreshTrackingMetadataAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        var satir = entity.Satirlar.FirstOrDefault(x => x.Id == satirId)
            ?? throw new BaseException("Sarf fişi satırı bulunamadı.", 404);

        satir.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SarfFisiDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var entity = await GetEditableEntityAsync(id, cancellationToken);
            if (entity.Satirlar.Count == 0)
            {
                throw new BaseException("Kesinleştirme için en az bir sarf fişi satırı olmalıdır.", 400);
            }

            foreach (var satir in entity.Satirlar)
            {
                if (satir.StokHareketId.HasValue)
                {
                    throw new BaseException("Bu sarf fişi zaten kesinleştirilmiş.", 400);
                }

                var hareket = await _stokHareketService.AddWithinCurrentTransactionAsync(BuildSarfHareketDto(entity, satir), cancellationToken);
                satir.StokHareketId = hareket.Id;
            }

            entity.Durum = SarfFisiDurumlari.Kesinlesti;
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

    public async Task<SarfFisiDto> IptalAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await GetEditableEntityAsync(id, cancellationToken);
        entity.Durum = SarfFisiDurumlari.Iptal;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<List<SarfBirimSecenekDto>> GetBirimlerAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }

        return await _dbContext.IsletmeAlanlari
            .AsNoTracking()
            .Include(x => x.IsletmeAlaniSinifi)
            .Include(x => x.Bina)
            .Where(x => x.AktifMi && x.Bina != null && x.Bina.TesisId == tesisId)
            .OrderBy(x => x.OzelAd ?? (x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty))
            .Select(x => new SarfBirimSecenekDto
            {
                Id = x.Id,
                Ad = x.OzelAd ?? (x.IsletmeAlaniSinifi != null ? x.IsletmeAlaniSinifi.Ad : string.Empty)
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<SarfFisi> BuildScopedQuery(DomainAccessScope scope)
    {
        var query = _dbContext.SarfFisleri
            .AsNoTracking()
            .Include(x => x.IsletmeAlani)
                .ThenInclude(x => x!.IsletmeAlaniSinifi)
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        return query;
    }

    private async Task<SarfFisi> GetEditableEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SarfFisleri
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Sarf fişi bulunamadı.", 404);

        await EnsureTesisAccessAsync(entity.TesisId, cancellationToken);

        if (!string.Equals(entity.Durum, SarfFisiDurumlari.Taslak, StringComparison.Ordinal))
        {
            throw new BaseException("Sadece taslak sarf fişleri değiştirilebilir.", 400);
        }

        return entity;
    }

    private async Task<SarfFisiDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken)
        => await GetByIdAsync(id) ?? throw new BaseException("Sarf fişi bulunamadı.", 404);

    private async Task<STYS.Muhasebe.Depolar.Entities.Depo> ResolveAndValidateDepoAsync(int depoId)
    {
        var depo = await _depoRepository.GetByIdAsync(depoId)
            ?? throw new BaseException("Seçilen depo bulunamadı.", 400);

        if (!depo.TesisId.HasValue)
        {
            throw new BaseException("Seçilen depo tesis bağlantısına sahip değil.", 400);
        }

        await EnsureTesisAccessAsync(depo.TesisId.Value, CancellationToken.None);
        return depo;
    }

    private async Task EnsureTesisAccessAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task ValidateIsletmeAlaniAsync(int? isletmeAlaniId, int tesisId, CancellationToken cancellationToken)
    {
        if (!isletmeAlaniId.HasValue)
        {
            return;
        }

        var exists = await _dbContext.IsletmeAlanlari
            .AsNoTracking()
            .Include(x => x.Bina)
            .AnyAsync(x => x.Id == isletmeAlaniId.Value && x.Bina != null && x.Bina.TesisId == tesisId, cancellationToken);

        if (!exists)
        {
            throw new BaseException("Seçilen birim sarf fişi deposu ile aynı tesise ait olmalıdır.", 400);
        }
    }

    private async Task RefreshTrackingMetadataAsync(SarfFisi entity, CancellationToken cancellationToken)
    {
        foreach (var satir in entity.Satirlar.Where(x => !x.IsDeleted))
        {
            satir.LotNo = null;
            satir.SonKullanmaTarihi = null;
            satir.SeriNo = null;

            if (satir.StokLotId.HasValue)
            {
                var lot = await _dbContext.StokLotlar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == satir.StokLotId.Value, cancellationToken)
                    ?? throw new BaseException("Seçilen lot bulunamadı.", 400);
                satir.LotNo = lot.LotNo;
                satir.SonKullanmaTarihi = lot.SonKullanmaTarihi;
            }

            if (satir.StokSeriId.HasValue)
            {
                var seri = await _dbContext.StokSeriler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == satir.StokSeriId.Value, cancellationToken)
                    ?? throw new BaseException("Seçilen seri bulunamadı.", 400);
                satir.SeriNo = seri.SeriNo;
            }
        }
    }

    private static void ValidateSatirInput(string takipTipi, decimal miktar, int? stokLotId, int? stokSeriId)
    {
        if (miktar <= 0)
        {
            throw new BaseException("Sarf miktarı 0'dan büyük olmalıdır.", 400);
        }

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Lot, StringComparison.Ordinal) && !stokLotId.HasValue)
        {
            throw new BaseException("Lot takipli taşınır kartta lot seçimi zorunludur.", 400);
        }

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal))
        {
            if (!stokSeriId.HasValue)
            {
                throw new BaseException("Seri takipli taşınır kartta seri seçimi zorunludur.", 400);
            }

            if (miktar != 1)
            {
                throw new BaseException("Seri takipli taşınır kartlarda miktar 1 olmalıdır.", 400);
            }
        }
    }

    private static string ResolveTakipTipi(TasinirKart kart)
        => TasinirKartServiceHelpers.ResolveTakipTipi(kart.TakipTipi, kart.TakipliMi);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static StokHareketDto BuildSarfHareketDto(SarfFisi fis, SarfFisiSatir satir)
        => new()
        {
            DepoId = fis.DepoId,
            TasinirKartId = satir.TasinirKartId,
            HareketTarihi = fis.SarfTarihi,
            HareketTipi = StokHareketTipleri.Sarf,
            Miktar = satir.Miktar,
            BirimFiyat = 0,
            Tutar = 0,
            BelgeTarihi = fis.SarfTarihi,
            Aciklama = satir.Aciklama ?? fis.Aciklama,
            KaynakModul = "SarfFisiSatir",
            KaynakId = satir.Id,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            StokLotId = satir.StokLotId,
            StokSeriId = satir.StokSeriId
        };
}
