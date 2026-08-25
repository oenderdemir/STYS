using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.KantinSatislari.Dtos;
using STYS.KantinYonetimi.KantinSatislari.Entities;
using STYS.KantinYonetimi.KantinSatislari.Repositories;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokHareketleri.Services;
using STYS.Muhasebe.StokMaliyetPolitikalari.Services;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Muhasebe.TasinirKartlari.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;
using System.Data;

namespace STYS.KantinYonetimi.KantinSatislari.Services;

public class KantinSatisService : BaseRdbmsService<KantinSatisDto, KantinSatis, int>, IKantinSatisService
{
    private const string KantinSatisKaynakModulu = "KantinSatisSatir";
    private const string KantinSatisIptalKaynakModulu = "KantinSatisIptal";

    private readonly StysAppDbContext _dbContext;
    private readonly IKantinSatisRepository _repository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IStokHareketService _stokHareketService;
    private readonly ITahsilatOdemeBelgesiService _tahsilatOdemeBelgesiService;
    private readonly IMuhasebeFisService _muhasebeFisService;
    private readonly IStokMaliyetKatmaniRestoreService _stokMaliyetKatmaniRestoreService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IMapper _mapper;

    public KantinSatisService(
        StysAppDbContext dbContext,
        IKantinSatisRepository repository,
        IUserAccessScopeService userAccessScopeService,
        IStokHareketService stokHareketService,
        ITahsilatOdemeBelgesiService tahsilatOdemeBelgesiService,
        IMuhasebeFisService muhasebeFisService,
        IStokMaliyetKatmaniRestoreService stokMaliyetKatmaniRestoreService,
        ICurrentUserAccessor currentUserAccessor,
        IMapper mapper)
        : base(repository, mapper)
    {
        _dbContext = dbContext;
        _repository = repository;
        _userAccessScopeService = userAccessScopeService;
        _stokHareketService = stokHareketService;
        _tahsilatOdemeBelgesiService = tahsilatOdemeBelgesiService;
        _muhasebeFisService = muhasebeFisService;
        _stokMaliyetKatmaniRestoreService = stokMaliyetKatmaniRestoreService;
        _currentUserAccessor = currentUserAccessor;
        _mapper = mapper;
    }

    public async Task<List<KantinSatisDto>> GetListAsync(int? tesisId, int? kantinId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildScopedQuery(scope);

        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        if (kantinId.HasValue && kantinId.Value > 0)
        {
            query = query.Where(x => x.KantinId == kantinId.Value);
        }

        var items = await query
            .OrderByDescending(x => x.SatisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return items.Select(MapDto).ToList();
    }

    public Task<KantinSatisDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, include: null, cancellationToken);

    public override Task<KantinSatisDto?> GetByIdAsync(int id, Func<IQueryable<KantinSatis>, IQueryable<KantinSatis>>? include = null)
        => GetByIdAsync(id, include, CancellationToken.None);

    public async Task<KantinSatisDto?> GetByIdAsync(int id, Func<IQueryable<KantinSatis>, IQueryable<KantinSatis>>? include, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildScopedQuery(scope);
        if (include is not null)
        {
            query = include(query);
        }

        var entity = await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : MapDto(entity);
    }

    public override async Task<IEnumerable<KantinSatisDto>> GetAllAsync(Func<IQueryable<KantinSatis>, IQueryable<KantinSatis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .OrderByDescending(x => x.SatisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return items.Select(MapDto).ToList();
    }

    public override async Task<IEnumerable<KantinSatisDto>> WhereAsync(System.Linq.Expressions.Expression<Func<KantinSatis, bool>> predicate, Func<IQueryable<KantinSatis>, IQueryable<KantinSatis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedQuery(scope)
            .Where(predicate)
            .OrderByDescending(x => x.SatisTarihi)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return items.Select(MapDto).ToList();
    }

    public override Task<KantinSatisDto> AddAsync(KantinSatisDto dto)
        => AddAsync(dto, CancellationToken.None);

    public async Task<KantinSatisDto> AddAsync(KantinSatisDto dto, CancellationToken cancellationToken = default)
    {
        var kantin = await GetRequiredActiveKantinAsync(dto.KantinId, cancellationToken);
        await GetRequiredActiveSatisNoktasiAsync(dto.SatisNoktasiId, dto.KantinId, cancellationToken);
        dto.TesisId = kantin.TesisId;
        dto.SatisTarihi = dto.SatisTarihi == default ? DateTime.UtcNow : dto.SatisTarihi;
        dto.Durum = KantinSatisDurumlari.Taslak;
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);
        RecalculateTotals(dto);

        var entity = _mapper.Map<KantinSatis>(dto);
        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public override Task<KantinSatisDto> UpdateAsync(KantinSatisDto dto)
        => UpdateAsync(dto, CancellationToken.None);

    public async Task<KantinSatisDto> UpdateAsync(KantinSatisDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kantin satış id zorunludur.", 400);
        }

        var entity = await GetEditableEntityAsync(dto.Id.Value, cancellationToken);
        entity.SatisTarihi = dto.SatisTarihi == default ? entity.SatisTarihi : dto.SatisTarihi;
        entity.Aciklama = NormalizeOptional(dto.Aciklama, 1024);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(entity.Id, cancellationToken);
    }

    public async Task<KantinSatisDto> AddSatirAsync(int satisId, AddKantinSatisSatirRequest request, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var kantin = await GetRequiredActiveKantinAsync(satis.KantinId, cancellationToken);
        var projection = await BuildSatirProjectionAsync(kantin, request.KantinUrunId, request.Miktar, request.StokLotId, request.StokSeriId, cancellationToken);

        satis.Satirlar.Add(new KantinSatisSatir
        {
            KantinSatisId = satis.Id,
            KantinUrunId = projection.KantinUrun.Id,
            TasinirKartId = projection.KantinUrun.TasinirKartId,
            Miktar = projection.Miktar,
            BirimSatisFiyati = projection.BirimSatisFiyati,
            KdvOrani = projection.KdvOrani,
            Matrah = projection.Matrah,
            KdvTutari = projection.KdvTutari,
            ToplamTutar = projection.ToplamTutar,
            StokLotId = projection.StokLotId,
            StokSeriId = projection.StokSeriId,
            Barkod = projection.Barkod,
            StokKodu = projection.StokKodu,
            UrunAdi = projection.UrunAdi,
            Birim = projection.Birim,
            TakipTipi = projection.TakipTipi,
            LotNo = projection.LotNo,
            SonKullanmaTarihi = projection.SonKullanmaTarihi,
            SeriNo = projection.SeriNo
        });

        RecalculateTotals(satis);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(satis.Id, cancellationToken);
    }

    public async Task<KantinSatisDto> UpdateSatirAsync(int satisId, int satirId, UpdateKantinSatisSatirRequest request, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var kantin = await GetRequiredActiveKantinAsync(satis.KantinId, cancellationToken);
        var satir = satis.Satirlar.FirstOrDefault(x => x.Id == satirId)
            ?? throw new BaseException("Kantin satış satırı bulunamadı.", 404);

        var projection = await BuildSatirProjectionAsync(kantin, request.KantinUrunId, request.Miktar, request.StokLotId, request.StokSeriId, cancellationToken);
        satir.KantinUrunId = projection.KantinUrun.Id;
        satir.TasinirKartId = projection.KantinUrun.TasinirKartId;
        satir.Miktar = projection.Miktar;
        satir.BirimSatisFiyati = projection.BirimSatisFiyati;
        satir.KdvOrani = projection.KdvOrani;
        satir.Matrah = projection.Matrah;
        satir.KdvTutari = projection.KdvTutari;
        satir.ToplamTutar = projection.ToplamTutar;
        satir.StokLotId = projection.StokLotId;
        satir.StokSeriId = projection.StokSeriId;
        satir.Barkod = projection.Barkod;
        satir.StokKodu = projection.StokKodu;
        satir.UrunAdi = projection.UrunAdi;
        satir.Birim = projection.Birim;
        satir.TakipTipi = projection.TakipTipi;
        satir.LotNo = projection.LotNo;
        satir.SonKullanmaTarihi = projection.SonKullanmaTarihi;
        satir.SeriNo = projection.SeriNo;

        RecalculateTotals(satis);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(satis.Id, cancellationToken);
    }

    public async Task DeleteSatirAsync(int satisId, int satirId, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var satir = satis.Satirlar.FirstOrDefault(x => x.Id == satirId)
            ?? throw new BaseException("Kantin satış satırı bulunamadı.", 404);

        satir.IsDeleted = true;
        RecalculateTotals(satis);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KantinSatisDto> AddOdemeAsync(int satisId, AddKantinSatisOdemeRequest request, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var kantin = await GetRequiredActiveKantinAsync(satis.KantinId, cancellationToken);
        var satisNoktasi = await GetRequiredActiveSatisNoktasiAsync(satis.SatisNoktasiId, satis.KantinId, cancellationToken);
        var projection = await BuildOdemeProjectionAsync(satisNoktasi, kantin.TesisId, request.OdemeYontemi, request.KasaBankaHesapId, request.Tutar, cancellationToken);

        satis.Odemeler.Add(new KantinSatisOdeme
        {
            KantinSatisId = satis.Id,
            OdemeYontemi = projection.OdemeYontemi,
            KasaBankaHesapId = projection.KasaBankaHesapId,
            Tutar = projection.Tutar,
            HesapKodSnapshot = projection.HesapKodSnapshot,
            HesapAdSnapshot = projection.HesapAdSnapshot
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(satis.Id, cancellationToken);
    }

    public async Task<KantinSatisDto> UpdateOdemeAsync(int satisId, int odemeId, UpdateKantinSatisOdemeRequest request, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var kantin = await GetRequiredActiveKantinAsync(satis.KantinId, cancellationToken);
        var satisNoktasi = await GetRequiredActiveSatisNoktasiAsync(satis.SatisNoktasiId, satis.KantinId, cancellationToken);
        var odeme = satis.Odemeler.FirstOrDefault(x => x.Id == odemeId)
            ?? throw new BaseException("Kantin satış ödeme satırı bulunamadı.", 404);

        var projection = await BuildOdemeProjectionAsync(satisNoktasi, kantin.TesisId, request.OdemeYontemi, request.KasaBankaHesapId, request.Tutar, cancellationToken);
        odeme.OdemeYontemi = projection.OdemeYontemi;
        odeme.KasaBankaHesapId = projection.KasaBankaHesapId;
        odeme.Tutar = projection.Tutar;
        odeme.HesapKodSnapshot = projection.HesapKodSnapshot;
        odeme.HesapAdSnapshot = projection.HesapAdSnapshot;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredDtoAsync(satis.Id, cancellationToken);
    }

    public async Task DeleteOdemeAsync(int satisId, int odemeId, CancellationToken cancellationToken = default)
    {
        var satis = await GetEditableEntityAsync(satisId, cancellationToken);
        var odeme = satis.Odemeler.FirstOrDefault(x => x.Id == odemeId)
            ?? throw new BaseException("Kantin satış ödeme satırı bulunamadı.", 404);

        odeme.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<KantinSatisBarkodUrunDto?> GetAktifUrunByBarkodAsync(int kantinId, string barkod, CancellationToken cancellationToken = default)
    {
        var normalizedBarkod = NormalizeBarcode(barkod);
        if (string.IsNullOrWhiteSpace(normalizedBarkod))
        {
            return null;
        }

        var kantin = await GetRequiredActiveKantinAsync(kantinId, cancellationToken);
        var urun = await _dbContext.KantinUrunler
            .AsNoTracking()
            .Include(x => x.TasinirKart)
            .FirstOrDefaultAsync(x => x.KantinId == kantin.Id && !x.IsDeleted && x.AktifMi && x.Barkod == normalizedBarkod, cancellationToken);

        if (urun is null || urun.TasinirKart is null)
        {
            return null;
        }

        var stok = await GetCurrentStockAsync(kantin.DepoId, urun.TasinirKartId, cancellationToken);
        return new KantinSatisBarkodUrunDto
        {
            KantinUrunId = urun.Id,
            TasinirKartId = urun.TasinirKartId,
            StokKodu = urun.TasinirKart.StokKodu,
            UrunAdi = urun.TasinirKart.Ad,
            Birim = urun.TasinirKart.Birim,
            Barkod = urun.Barkod,
            SatisFiyati = urun.SatisFiyati,
            KdvOrani = urun.TasinirKart.KdvOrani,
            MevcutStok = stok,
            TakipTipi = ResolveTakipTipi(urun.TasinirKart)
        };
    }

    public async Task<KantinSatisDto> KesinlestirAsync(int satisId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var satis = await _dbContext.KantinSatislar
                .Include(x => x.Kantin)
                .Include(x => x.SatisNoktasi)
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                .FirstOrDefaultAsync(x => x.Id == satisId && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

            await EnsureTesisAccessAsync(satis.TesisId, cancellationToken);

            if (string.Equals(satis.Durum, KantinSatisDurumlari.Kesinlesti, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return await GetRequiredDtoAsync(satis.Id, cancellationToken);
            }

            if (satis.Satirlar.Count == 0)
            {
                throw new BaseException("Kesinleştirme için en az bir satış satırı olmalıdır.", 400);
            }

            var kantin = await GetRequiredActiveKantinAsync(satis.KantinId, cancellationToken);
            var satisNoktasi = await GetRequiredActiveSatisNoktasiAsync(satis.SatisNoktasiId, satis.KantinId, cancellationToken);
            if (!kantin.PerakendeCariKartId.HasValue)
            {
                throw new BaseException("Kantin satışının kesinleşmesi için Perakende Cari seçimi zorunludur.", 400);
            }

            ValidateDraftConsistency(satis);
            await ValidateOdemelerAsync(satisNoktasi, kantin.TesisId, satis, assignResolvedAccounts: true, cancellationToken);

            foreach (var satir in satis.Satirlar)
            {
                var projection = await BuildSatirProjectionAsync(kantin, satir.KantinUrunId, satir.Miktar, satir.StokLotId, satir.StokSeriId, cancellationToken);
                ApplySatirProjection(satir, projection);

                var existingLinkedMovement = await _dbContext.StokHareketleri
                    .AsNoTracking()
                    .AnyAsync(x =>
                        !x.IsDeleted &&
                        x.Durum == StokHareketDurumlari.Aktif &&
                        x.KaynakModul == KantinSatisKaynakModulu &&
                        x.KaynakId == satir.Id,
                        cancellationToken);

                if (existingLinkedMovement)
                {
                    throw new BaseException("Bu satış satırı için daha önce stok çıkışı oluşturulmuş.", 400);
                }

                var hareket = await _stokHareketService.AddWithinCurrentTransactionAsync(BuildStokHareketDto(kantin, satis, satir), cancellationToken);
                satir.StokHareketId = hareket.Id;
            }

            RecalculateTotals(satis);
            ValidatePaymentSum(satis);
            await EnsurePerakendeCariIsValidAsync(kantin, cancellationToken);
            await EnsureTahsilatlarAsync(kantin, satis, cancellationToken);

            satis.Durum = KantinSatisDurumlari.Kesinlesti;
            satis.KesinlesmeTarihi = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetRequiredDtoAsync(satis.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<KantinSatisDto> IptalEtAsync(int satisId, string aciklama, CancellationToken cancellationToken = default)
    {
        var normalizedAciklama = NormalizeOptional(aciklama, 1024);
        if (string.IsNullOrWhiteSpace(normalizedAciklama))
        {
            throw new BaseException("Satış iptali için açıklama zorunludur.", 400);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var satis = await _dbContext.KantinSatislar
                .Include(x => x.Kantin)
                .Include(x => x.SatisNoktasi)
                .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
                    .ThenInclude(x => x.StokHareket)
                .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                    .ThenInclude(x => x.TahsilatOdemeBelgesi)
                .FirstOrDefaultAsync(x => x.Id == satisId && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

            await EnsureTesisAccessAsync(satis.TesisId, cancellationToken);

            if (string.Equals(satis.Durum, KantinSatisDurumlari.IptalEdildi, StringComparison.Ordinal))
            {
                await transaction.CommitAsync(cancellationToken);
                return await GetRequiredDtoAsync(satis.Id, cancellationToken);
            }

            if (!string.Equals(satis.Durum, KantinSatisDurumlari.Kesinlesti, StringComparison.Ordinal))
            {
                throw new BaseException("Yalnızca kesinleşmiş satışlar iptal edilebilir.", 400);
            }

            await ValidateIptalOnKosullariAsync(satis, cancellationToken);

            var iptalZamani = DateTime.UtcNow;

            foreach (var satir in satis.Satirlar.OrderBy(x => x.Id))
            {
                var originalMovement = satir.StokHareket
                    ?? throw new BaseException("Satış satırına bağlı stok hareketi bulunamadı.", 400);

                var reversal = await _stokHareketService.AddWithinCurrentTransactionAsync(
                    BuildIptalStokHareketDto(satis, satir, originalMovement, iptalZamani),
                    cancellationToken);

                await _stokMaliyetKatmaniRestoreService.RestoreLayeredCostIfNeededAsync(originalMovement, reversal, cancellationToken);
                satir.IptalStokHareketId = reversal.Id;
            }

            foreach (var odeme in satis.Odemeler.OrderBy(x => x.Id))
            {
                await _tahsilatOdemeBelgesiService.IptalEtManagedSourceWithinCurrentTransactionAsync(
                    odeme.TahsilatOdemeBelgesiId!.Value,
                    MuhasebeKaynakModulleri.KantinSatisOdeme,
                    odeme.Id,
                    cancellationToken);
            }

            await MuhasebeFisiniKapatAsync(satis, normalizedAciklama, cancellationToken);

            satis.Durum = KantinSatisDurumlari.IptalEdildi;
            satis.IptalTarihi = iptalZamani;
            satis.IptalAciklamasi = normalizedAciklama;
            satis.IptalEdenKullaniciId = _currentUserAccessor.GetCurrentUserId()?.ToString();

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await GetRequiredDtoAsync(satis.Id, cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private IQueryable<KantinSatis> BuildScopedQuery(DomainAccessScope scope)
    {
        var query = _dbContext.KantinSatislar
            .AsNoTracking()
            .Include(x => x.Kantin)
            .Include(x => x.SatisNoktasi)
            .Include(x => x.MuhasebeFis)
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                .ThenInclude(x => x.TahsilatOdemeBelgesi)
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        return query;
    }

    private async Task<KantinSatis> GetEditableEntityAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.KantinSatislar
            .Include(x => x.Kantin)
            .Include(x => x.SatisNoktasi)
            .Include(x => x.MuhasebeFis)
            .Include(x => x.Satirlar.Where(s => !s.IsDeleted))
            .Include(x => x.Odemeler.Where(o => !o.IsDeleted))
                .ThenInclude(x => x.TahsilatOdemeBelgesi)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

        await EnsureTesisAccessAsync(entity.TesisId, cancellationToken);

        if (!string.Equals(entity.Durum, KantinSatisDurumlari.Taslak, StringComparison.Ordinal))
        {
            throw new BaseException("Kesinleşmiş kantin satışları değiştirilemez.", 400);
        }

        return entity;
    }

    private async Task<Kantin> GetRequiredActiveKantinAsync(int kantinId, CancellationToken cancellationToken)
    {
        var kantin = await _dbContext.Kantinler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kantinId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin bulunamadı.", 404);

        await EnsureTesisAccessAsync(kantin.TesisId, cancellationToken);
        if (!kantin.AktifMi)
        {
            throw new BaseException("Satış için kantin aktif olmalıdır.", 400);
        }

        return kantin;
    }

    private async Task<KantinSatisNoktasi> GetRequiredActiveSatisNoktasiAsync(int satisNoktasiId, int kantinId, CancellationToken cancellationToken)
    {
        var satisNoktasi = await _dbContext.KantinSatisNoktalari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == satisNoktasiId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Satış noktası bulunamadı.", 404);

        if (satisNoktasi.KantinId != kantinId)
        {
            throw new BaseException("Satış noktası seçilen kantine ait olmalıdır.", 400);
        }

        if (!satisNoktasi.AktifMi)
        {
            throw new BaseException("Satış için satış noktası aktif olmalıdır.", 400);
        }

        return satisNoktasi;
    }

    private async Task<KantinSatisDto> GetRequiredDtoAsync(int id, CancellationToken cancellationToken)
        => await GetByIdAsync(id, cancellationToken) ?? throw new BaseException("Kantin satışı bulunamadı.", 404);

    private async Task EnsureTesisAccessAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task<SatirProjection> BuildSatirProjectionAsync(Kantin kantin, int kantinUrunId, decimal miktar, int? stokLotId, int? stokSeriId, CancellationToken cancellationToken)
    {
        if (miktar <= 0)
        {
            throw new BaseException("Satış miktarı 0'dan büyük olmalıdır.", 400);
        }

        var urun = await _dbContext.KantinUrunler
            .AsNoTracking()
            .Include(x => x.TasinirKart)
            .FirstOrDefaultAsync(x => x.Id == kantinUrunId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin ürünü bulunamadı.", 404);

        if (urun.KantinId != kantin.Id)
        {
            throw new BaseException("Seçilen ürün satış yapılan kantine ait olmalıdır.", 400);
        }

        if (!urun.AktifMi)
        {
            throw new BaseException("Seçilen kantin ürünü aktif olmalıdır.", 400);
        }

        var kart = urun.TasinirKart ?? throw new BaseException("Kantin ürünü taşınır kartı bulunamadı.", 400);
        if (!kart.TesisId.HasValue || kart.TesisId.Value != kantin.TesisId || !kart.AktifMi)
        {
            throw new BaseException("Seçilen ürün taşınır kartı satış için geçerli değil.", 400);
        }

        var takipTipi = ResolveTakipTipi(kart);
        string? lotNo = null;
        DateTime? sonKullanmaTarihi = null;
        string? seriNo = null;

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Lot, StringComparison.Ordinal))
        {
            if (!stokLotId.HasValue)
            {
                throw new BaseException("Lot takipli ürünlerde lot seçimi zorunludur.", 400);
            }

            var lot = await _dbContext.StokLotlar
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == stokLotId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Seçilen lot bulunamadı.", 400);

            if (lot.TesisId != kantin.TesisId || lot.TasinirKartId != kart.Id)
            {
                throw new BaseException("Seçilen lot ürün ile uyumlu değil.", 400);
            }

            lotNo = lot.LotNo;
            sonKullanmaTarihi = lot.SonKullanmaTarihi;
        }
        else if (stokLotId.HasValue)
        {
            throw new BaseException("Takipsiz veya seri takipli üründe lot seçimi yapılamaz.", 400);
        }

        if (string.Equals(takipTipi, TasinirKartTakipTipleri.Seri, StringComparison.Ordinal))
        {
            if (!stokSeriId.HasValue)
            {
                throw new BaseException("Seri takipli ürünlerde seri seçimi zorunludur.", 400);
            }

            if (miktar != 1)
            {
                throw new BaseException("Seri takipli ürünlerde miktar 1 olmalıdır.", 400);
            }

            var seri = await _dbContext.StokSeriler
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == stokSeriId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Seçilen seri bulunamadı.", 400);

            if (seri.TesisId != kantin.TesisId || seri.TasinirKartId != kart.Id)
            {
                throw new BaseException("Seçilen seri ürün ile uyumlu değil.", 400);
            }

            seriNo = seri.SeriNo;
        }
        else if (stokSeriId.HasValue)
        {
            throw new BaseException("Takipsiz veya lot takipli üründe seri seçimi yapılamaz.", 400);
        }

        var birimSatisFiyati = ParaTutarYuvarlamaHelper.Yuvarla(urun.SatisFiyati);
        var toplamTutar = ParaTutarYuvarlamaHelper.Yuvarla(miktar * birimSatisFiyati);
        var kdvTutari = ParaTutarYuvarlamaHelper.Yuvarla(toplamTutar * kart.KdvOrani / (100m + kart.KdvOrani));
        var matrah = ParaTutarYuvarlamaHelper.Yuvarla(toplamTutar - kdvTutari);

        return new SatirProjection(
            urun,
            miktar,
            birimSatisFiyati,
            kart.KdvOrani,
            matrah,
            kdvTutari,
            toplamTutar,
            stokLotId,
            stokSeriId,
            urun.Barkod,
            kart.StokKodu,
            kart.Ad,
            kart.Birim,
            takipTipi,
            lotNo,
            sonKullanmaTarihi,
            seriNo);
    }

    private async Task<OdemeProjection> BuildOdemeProjectionAsync(KantinSatisNoktasi satisNoktasi, int tesisId, string odemeYontemi, int? kasaBankaHesapId, decimal tutar, CancellationToken cancellationToken)
    {
        if (tutar <= 0)
        {
            throw new BaseException("Ödeme tutarı 0'dan büyük olmalıdır.", 400);
        }

        var normalizedYontem = NormalizeRequired(odemeYontemi, "Ödeme yöntemi zorunludur.", 32);
        if (!string.Equals(normalizedYontem, OdemeYontemleri.Nakit, StringComparison.Ordinal)
            && !string.Equals(normalizedYontem, OdemeYontemleri.KrediKarti, StringComparison.Ordinal))
        {
            throw new BaseException("Kantin satışında yalnızca Nakit veya KrediKarti ödeme yöntemi kullanılabilir.", 400);
        }

        KasaBankaHesap? hesap = null;
        if (string.Equals(normalizedYontem, OdemeYontemleri.Nakit, StringComparison.Ordinal))
        {
            var effectiveHesapId = kasaBankaHesapId ?? satisNoktasi.VarsayilanNakitKasaId;
            if (!effectiveHesapId.HasValue)
            {
                throw new BaseException("Nakit ödeme için kasa seçimi zorunludur.", 400);
            }

            hesap = await ResolveValidHesapAsync(tesisId, effectiveHesapId.Value, KasaBankaHesapTipleri.NakitKasa, "Nakit", cancellationToken);
        }
        else
        {
            var effectiveHesapId = kasaBankaHesapId ?? satisNoktasi.VarsayilanPosHesapId;
            if (!effectiveHesapId.HasValue)
            {
                throw new BaseException("Kredi kartı ödeme için POS hesabı seçimi zorunludur.", 400);
            }

            hesap = await ResolveValidHesapAsync(tesisId, effectiveHesapId.Value, KasaBankaHesapTipleri.KrediKarti, "Kredi kartı", cancellationToken);
        }

        return new OdemeProjection(
            normalizedYontem,
            hesap?.Id,
            ParaTutarYuvarlamaHelper.Yuvarla(tutar),
            hesap?.Kod,
            hesap?.Ad);
    }

    private async Task<KasaBankaHesap> ResolveValidHesapAsync(int tesisId, int hesapId, string beklenenTip, string odemeLabel, CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.KasaBankaHesaplari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == hesapId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Seçilen ödeme hesabı bulunamadı.", 400);

        if (hesap.TesisId != tesisId)
        {
            throw new BaseException("Seçilen ödeme hesabı satış ile aynı tesise ait olmalıdır.", 400);
        }

        if (!hesap.AktifMi)
        {
            throw new BaseException("Seçilen ödeme hesabı aktif olmalıdır.", 400);
        }

        if (!string.Equals(hesap.Tip, beklenenTip, StringComparison.Ordinal))
        {
            throw new BaseException($"{odemeLabel} ödeme hesabı tipi geçersiz.", 400);
        }

        return hesap;
    }

    private async Task EnsurePerakendeCariIsValidAsync(Kantin kantin, CancellationToken cancellationToken)
    {
        var perakendeCariKartId = kantin.PerakendeCariKartId
            ?? throw new BaseException("Kantin satışının kesinleşmesi için Perakende Cari seçimi zorunludur.", 400);

        var cari = await _dbContext.CariKartlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == perakendeCariKartId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Seçilen perakende cari bulunamadı.", 400);

        if (cari.TesisId != kantin.TesisId)
        {
            throw new BaseException("Seçilen perakende cari kantin ile aynı tesise ait olmalıdır.", 400);
        }

        if (!cari.AktifMi)
        {
            throw new BaseException("Seçilen perakende cari aktif olmalıdır.", 400);
        }

        if (!string.Equals(cari.CariTipi, CariKartTipleri.Musteri, StringComparison.Ordinal)
            && !string.Equals(cari.CariTipi, CariKartTipleri.KurumsalMusteri, StringComparison.Ordinal))
        {
            throw new BaseException("Perakende cari yalnızca müşteri veya kurumsal müşteri tipinde olabilir.", 400);
        }
    }

    private async Task EnsureTahsilatlarAsync(Kantin kantin, KantinSatis satis, CancellationToken cancellationToken)
    {
        foreach (var odeme in satis.Odemeler.Where(x => !x.IsDeleted).OrderBy(x => x.Id))
        {
            if (odeme.TahsilatOdemeBelgesiId.HasValue)
            {
                continue;
            }

            var existingBelge = await _dbContext.TahsilatOdemeBelgeleri
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    !x.IsDeleted &&
                    x.KaynakModul == MuhasebeKaynakModulleri.KantinSatisOdeme &&
                    x.KaynakId == odeme.Id,
                    cancellationToken);
            if (existingBelge is not null)
            {
                ValidateExistingKantinTahsilati(existingBelge, kantin.PerakendeCariKartId!.Value, odeme);
                odeme.TahsilatOdemeBelgesiId = existingBelge.Id;
                continue;
            }

            var belge = await _tahsilatOdemeBelgesiService.AddWithinCurrentTransactionAsync(
                new TahsilatOdemeBelgesiDto
                {
                    BelgeNo = BuildTahsilatBelgeNo(kantin.TesisId, satis.Id, odeme.Id),
                    BelgeTarihi = satis.SatisTarihi,
                    BelgeTipi = TahsilatOdemeBelgeTipleri.Tahsilat,
                    CariKartId = kantin.PerakendeCariKartId!.Value,
                    Tutar = odeme.Tutar,
                    ParaBirimi = "TRY",
                    OdemeYontemi = odeme.OdemeYontemi,
                    Aciklama = $"Kantin satış tahsilatı #{satis.Id}",
                    KaynakModul = MuhasebeKaynakModulleri.KantinSatisOdeme,
                    KaynakId = odeme.Id,
                    KapatilacakCariHareketId = null,
                    Durum = TahsilatOdemeBelgeDurumlari.Aktif,
                    KasaBankaHesapId = odeme.KasaBankaHesapId
                },
                requireCariMuhasebeHesabi: false,
                cancellationToken);

            odeme.TahsilatOdemeBelgesiId = belge.Id;
        }
    }

    private static void ValidateExistingKantinTahsilati(TahsilatOdemeBelgesi existingBelge, int perakendeCariKartId, KantinSatisOdeme odeme)
    {
        if (!string.Equals(existingBelge.Durum, TahsilatOdemeBelgeDurumlari.Aktif, StringComparison.Ordinal)
            || !string.Equals(existingBelge.BelgeTipi, TahsilatOdemeBelgeTipleri.Tahsilat, StringComparison.Ordinal)
            || existingBelge.CariKartId != perakendeCariKartId
            || existingBelge.Tutar != odeme.Tutar
            || !string.Equals(existingBelge.OdemeYontemi, odeme.OdemeYontemi, StringComparison.Ordinal)
            || existingBelge.KasaBankaHesapId != odeme.KasaBankaHesapId
            || !string.Equals(existingBelge.ParaBirimi, "TRY", StringComparison.Ordinal))
        {
            throw new BaseException("Mevcut kantin tahsilat belgesi ödeme bilgileriyle uyumsuz.", 400);
        }
    }

    private async Task ValidateOdemelerAsync(KantinSatisNoktasi satisNoktasi, int tesisId, KantinSatis satis, bool assignResolvedAccounts, CancellationToken cancellationToken)
    {
        if (satis.Odemeler.Count == 0)
        {
            throw new BaseException("Kesinleştirme için en az bir ödeme satırı olmalıdır.", 400);
        }

        foreach (var odeme in satis.Odemeler)
        {
            var projection = await BuildOdemeProjectionAsync(satisNoktasi, tesisId, odeme.OdemeYontemi, odeme.KasaBankaHesapId, odeme.Tutar, cancellationToken);
            odeme.OdemeYontemi = projection.OdemeYontemi;
            odeme.Tutar = projection.Tutar;
            if (assignResolvedAccounts)
            {
                odeme.KasaBankaHesapId = projection.KasaBankaHesapId;
                odeme.HesapKodSnapshot = projection.HesapKodSnapshot;
                odeme.HesapAdSnapshot = projection.HesapAdSnapshot;
            }
        }
    }

    private static void ValidateDraftConsistency(KantinSatis satis)
    {
        if (satis.Satirlar.Any(x => x.Miktar <= 0))
        {
            throw new BaseException("Satış satır miktarı 0'dan büyük olmalıdır.", 400);
        }

        if (satis.Satirlar.Any(x => x.StokHareketId.HasValue))
        {
            throw new BaseException("Taslak satış tutarsız stok hareketi bağlantısına sahip.", 400);
        }
    }

    private static void ValidatePaymentSum(KantinSatis satis)
    {
        var toplamOdeme = ParaTutarYuvarlamaHelper.Yuvarla(satis.Odemeler.Where(x => !x.IsDeleted).Sum(x => x.Tutar));
        var toplamSatis = ParaTutarYuvarlamaHelper.Yuvarla(satis.ToplamTutar);
        if (toplamOdeme != toplamSatis)
        {
            throw new BaseException("Ödeme toplamı satış toplamına eşit olmalıdır.", 400);
        }
    }

    private static void RecalculateTotals(KantinSatisDto dto)
    {
        dto.ToplamTutar = ParaTutarYuvarlamaHelper.Yuvarla(dto.Satirlar.Sum(x => x.ToplamTutar));
        dto.KdvToplami = ParaTutarYuvarlamaHelper.Yuvarla(dto.Satirlar.Sum(x => x.KdvTutari));
        dto.MatrahToplami = ParaTutarYuvarlamaHelper.Yuvarla(dto.Satirlar.Sum(x => x.Matrah));
    }

    private static void RecalculateTotals(KantinSatis entity)
    {
        entity.ToplamTutar = ParaTutarYuvarlamaHelper.Yuvarla(entity.Satirlar.Where(x => !x.IsDeleted).Sum(x => x.ToplamTutar));
        entity.KdvToplami = ParaTutarYuvarlamaHelper.Yuvarla(entity.Satirlar.Where(x => !x.IsDeleted).Sum(x => x.KdvTutari));
        entity.MatrahToplami = ParaTutarYuvarlamaHelper.Yuvarla(entity.Satirlar.Where(x => !x.IsDeleted).Sum(x => x.Matrah));
    }

    private static void ApplySatirProjection(KantinSatisSatir satir, SatirProjection projection)
    {
        satir.KantinUrunId = projection.KantinUrun.Id;
        satir.TasinirKartId = projection.KantinUrun.TasinirKartId;
        satir.Miktar = projection.Miktar;
        satir.BirimSatisFiyati = projection.BirimSatisFiyati;
        satir.KdvOrani = projection.KdvOrani;
        satir.Matrah = projection.Matrah;
        satir.KdvTutari = projection.KdvTutari;
        satir.ToplamTutar = projection.ToplamTutar;
        satir.StokLotId = projection.StokLotId;
        satir.StokSeriId = projection.StokSeriId;
        satir.Barkod = projection.Barkod;
        satir.StokKodu = projection.StokKodu;
        satir.UrunAdi = projection.UrunAdi;
        satir.Birim = projection.Birim;
        satir.TakipTipi = projection.TakipTipi;
        satir.LotNo = projection.LotNo;
        satir.SonKullanmaTarihi = projection.SonKullanmaTarihi;
        satir.SeriNo = projection.SeriNo;
    }

    private async Task ValidateIptalOnKosullariAsync(KantinSatis satis, CancellationToken cancellationToken)
    {
        // Cross-invariant: bu satış için kesinleşmiş (Kesinlesti) en az bir KantinSatisIade varsa tam
        // satış iptali reddedilir — kısmi iade ile stoğa dönmüş miktar, K3C1 full reversal tarafından
        // İKİNCİ KEZ geri eklenmesin. Kontrol service/domain seviyesinde ve iptal transaction'ı içinde.
        var kesinlesmisIadeVarMi = await _dbContext.KantinSatisIadeleri
            .AsNoTracking()
            .AnyAsync(x =>
                x.KantinSatisId == satis.Id
                && !x.IsDeleted
                && x.Durum == KantinSatisIadeDurumlari.Kesinlesti,
                cancellationToken);

        if (kesinlesmisIadeVarMi)
        {
            throw new BaseException("Bu satış için kesinleşmiş ürün iadesi bulunduğundan satış tamamen iptal edilemez.", 400);
        }

        if (satis.Satirlar.Count == 0)
        {
            throw new BaseException("İptal için aktif satış satırı bulunmalıdır.", 400);
        }

        foreach (var satir in satis.Satirlar)
        {
            if (!satir.StokHareketId.HasValue)
            {
                throw new BaseException("Satış satırına bağlı stok hareketi bulunamadı.", 400);
            }

            if (satir.IptalStokHareketId.HasValue)
            {
                throw new BaseException("Satış satırı için daha önce iptal stok hareketi oluşturulmuş.", 400);
            }

            var originalMovement = satir.StokHareket;
            if (originalMovement is null
                || originalMovement.IsDeleted
                || !string.Equals(originalMovement.Durum, StokHareketDurumlari.Aktif, StringComparison.Ordinal)
                || !string.Equals(originalMovement.KaynakModul, KantinSatisKaynakModulu, StringComparison.Ordinal)
                || originalMovement.KaynakId != satir.Id)
            {
                throw new BaseException("Satış satırının kaynak stok hareketi bütünlüğü bozuk.", 400);
            }

            var existingReversal = await _dbContext.StokHareketleri
                .AsNoTracking()
                .AnyAsync(x =>
                    !x.IsDeleted &&
                    x.Durum == StokHareketDurumlari.Aktif &&
                    x.KaynakModul == KantinSatisIptalKaynakModulu &&
                    x.KaynakId == satir.Id,
                    cancellationToken);

            if (existingReversal)
            {
                throw new BaseException("Satış satırı için daha önce iptal stok hareketi oluşturulmuş.", 400);
            }
        }

        foreach (var odeme in satis.Odemeler)
        {
            if (!odeme.TahsilatOdemeBelgesiId.HasValue)
            {
                throw new BaseException("Tüm ödemeler için tahsilat belgesi bulunmalıdır.", 400);
            }

            var belge = odeme.TahsilatOdemeBelgesi;
            if (belge is null
                || belge.IsDeleted
                || !string.Equals(belge.KaynakModul, MuhasebeKaynakModulleri.KantinSatisOdeme, StringComparison.Ordinal)
                || belge.KaynakId != odeme.Id)
            {
                throw new BaseException("Ödemenin tahsilat belgesi bütünlüğü bozuk.", 400);
            }
        }

        if (satis.MuhasebeFisId.HasValue)
        {
            var fis = await _dbContext.MuhasebeFisler
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == satis.MuhasebeFisId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Satışa bağlı muhasebe fişi bulunamadı.", 400);

            if (!string.Equals(fis.KaynakModul, MuhasebeKaynakModulleri.KantinSatis, StringComparison.Ordinal)
                || fis.KaynakId != satis.Id
                || fis.TesisId != satis.TesisId)
            {
                throw new BaseException("Satışa bağlı muhasebe fişi bütünlüğü bozuk.", 400);
            }
        }
    }

    private async Task MuhasebeFisiniKapatAsync(KantinSatis satis, string aciklama, CancellationToken cancellationToken)
    {
        if (!satis.MuhasebeFisId.HasValue)
        {
            return;
        }

        var fis = await _dbContext.MuhasebeFisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == satis.MuhasebeFisId.Value && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Satışa bağlı muhasebe fişi bulunamadı.", 400);

        if (!string.Equals(fis.KaynakModul, MuhasebeKaynakModulleri.KantinSatis, StringComparison.Ordinal)
            || fis.KaynakId != satis.Id)
        {
            throw new BaseException("Satışa bağlı muhasebe fişi bütünlüğü bozuk.", 400);
        }

        switch (fis.Durum)
        {
            case MuhasebeFisDurumlari.Taslak:
                await _muhasebeFisService.KantinSatisFisiniSilAsync(fis.Id, satis.Id, satis.TesisId, cancellationToken);
                return;

            case MuhasebeFisDurumlari.Onayli:
            case MuhasebeFisDurumlari.Iptal:
                await _muhasebeFisService.KantinSatisFisiIptalEtAsync(fis.Id, satis.Id, satis.TesisId, aciklama, cancellationToken);
                return;

            default:
                throw new BaseException($"Satışa bağlı muhasebe fişi beklenmeyen bir durumda ({fis.Durum}).", 400);
        }
    }

    private static StokHareketDto BuildIptalStokHareketDto(KantinSatis satis, KantinSatisSatir satir, StokHareket originalMovement, DateTime iptalZamani)
        => new()
        {
            DepoId = originalMovement.DepoId,
            TasinirKartId = originalMovement.TasinirKartId,
            HareketTarihi = iptalZamani,
            HareketTipi = StokHareketTipleri.Giris,
            Miktar = originalMovement.Miktar,
            BirimFiyat = originalMovement.BirimFiyat,
            Tutar = originalMovement.Tutar,
            BelgeTarihi = iptalZamani,
            Aciklama = $"Kantin Satışı #{satis.Id} iptal - {satir.StokKodu} {satir.UrunAdi}",
            KaynakModul = KantinSatisIptalKaynakModulu,
            KaynakId = satir.Id,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0,
            MaliyetBirimFiyat = originalMovement.MaliyetBirimFiyat,
            MaliyetTutari = originalMovement.MaliyetTutari,
            StokLotId = originalMovement.StokLotId,
            StokSeriId = originalMovement.StokSeriId
        };

    private static StokHareketDto BuildStokHareketDto(Kantin kantin, KantinSatis satis, KantinSatisSatir satir)
        => new()
        {
            DepoId = kantin.DepoId,
            TasinirKartId = satir.TasinirKartId,
            HareketTarihi = satis.SatisTarihi,
            HareketTipi = StokHareketTipleri.Cikis,
            Miktar = satir.Miktar,
            BirimFiyat = 0,
            Tutar = 0,
            BelgeTarihi = satis.SatisTarihi,
            Aciklama = $"Kantin Satışı #{satis.Id} - {satir.StokKodu} {satir.UrunAdi}",
            KaynakModul = KantinSatisKaynakModulu,
            KaynakId = satir.Id,
            Durum = StokHareketDurumlari.Aktif,
            KdvUygulamaTipi = (int)KdvUygulamaTipi.KdvKapsamDisi,
            KdvOrani = 0,
            KdvTutari = 0,
            StokLotId = satir.StokLotId,
            StokSeriId = satir.StokSeriId
        };

    private static string ResolveTakipTipi(TasinirKart kart)
        => TasinirKartServiceHelpers.ResolveTakipTipi(kart.TakipTipi, kart.TakipliMi);

    private static string NormalizeRequired(string? value, string errorMessage, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BaseException(errorMessage, 400);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is null)
        {
            return null;
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private static string? NormalizeBarcode(string? barkod)
        => NormalizeOptional(barkod, 128)?.ToUpperInvariant();

    private async Task<decimal> GetCurrentStockAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.StokHareketleri
            .AsNoTracking()
            .Where(x => x.DepoId == depoId && x.TasinirKartId == tasinirKartId && !x.IsDeleted && x.Durum == StokHareketDurumlari.Aktif)
            .Select(x => new { x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar })
            .ToListAsync(cancellationToken);

        return rows.Sum(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar
            : StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? -x.Miktar
            : 0m);
    }

    private static string BuildTahsilatBelgeNo(int tesisId, int satisId, int odemeId)
        => $"KNT-{tesisId}-{satisId}-{odemeId}";

    private static KantinSatisDto MapDto(KantinSatis entity)
    {
        var dto = new KantinSatisDto
        {
            Id = entity.Id,
            TesisId = entity.TesisId,
            KantinId = entity.KantinId,
            SatisNoktasiId = entity.SatisNoktasiId,
            SatisTarihi = entity.SatisTarihi,
            Durum = entity.Durum,
            ToplamTutar = entity.ToplamTutar,
            MatrahToplami = entity.MatrahToplami,
            KdvToplami = entity.KdvToplami,
            Aciklama = entity.Aciklama,
            KesinlesmeTarihi = entity.KesinlesmeTarihi,
            MuhasebeFisId = entity.MuhasebeFisId,
            MuhasebeFisNo = entity.MuhasebeFis?.FisNo,
            MuhasebeFisDurumu = entity.MuhasebeFis?.Durum,
            MuhasebeFisOlusturmaTarihi = entity.MuhasebeFisOlusturmaTarihi,
            IptalTarihi = entity.IptalTarihi,
            IptalAciklamasi = entity.IptalAciklamasi,
            IptalEdenKullaniciId = entity.IptalEdenKullaniciId,
            KantinKod = entity.Kantin?.Kod,
            KantinAd = entity.Kantin?.Ad,
            SatisNoktasiKod = entity.SatisNoktasi?.Kod,
            SatisNoktasiAd = entity.SatisNoktasi?.Ad,
            Satirlar = entity.Satirlar
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(x => new KantinSatisSatirDto
                {
                    Id = x.Id,
                    KantinSatisId = x.KantinSatisId,
                    KantinUrunId = x.KantinUrunId,
                    TasinirKartId = x.TasinirKartId,
                    Miktar = x.Miktar,
                    BirimSatisFiyati = x.BirimSatisFiyati,
                    KdvOrani = x.KdvOrani,
                    Matrah = x.Matrah,
                    KdvTutari = x.KdvTutari,
                    ToplamTutar = x.ToplamTutar,
                    StokLotId = x.StokLotId,
                    StokSeriId = x.StokSeriId,
                    StokHareketId = x.StokHareketId,
                    Barkod = x.Barkod,
                    StokKodu = x.StokKodu,
                    UrunAdi = x.UrunAdi,
                    Birim = x.Birim,
                    TakipTipi = x.TakipTipi,
                    LotNo = x.LotNo,
                    SonKullanmaTarihi = x.SonKullanmaTarihi,
                    SeriNo = x.SeriNo
                })
                .ToList(),
            Odemeler = entity.Odemeler
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Id)
                .Select(x => new KantinSatisOdemeDto
                {
                    Id = x.Id,
                    KantinSatisId = x.KantinSatisId,
                    OdemeYontemi = x.OdemeYontemi,
                    KasaBankaHesapId = x.KasaBankaHesapId,
                    TahsilatOdemeBelgesiId = x.TahsilatOdemeBelgesiId,
                    Tutar = x.Tutar,
                    HesapKodSnapshot = x.HesapKodSnapshot,
                    HesapAdSnapshot = x.HesapAdSnapshot,
                    TahsilatBelgeNo = x.TahsilatOdemeBelgesi?.BelgeNo
                })
                .ToList()
        };

        dto.OdemeOzeti = string.Join(" + ",
            dto.Odemeler
                .GroupBy(x => x.OdemeYontemi)
                .Select(g => $"{g.Key}: {ParaTutarYuvarlamaHelper.Yuvarla(g.Sum(x => x.Tutar)):0.00}"));

        return dto;
    }

    private sealed record SatirProjection(
        KantinUrun KantinUrun,
        decimal Miktar,
        decimal BirimSatisFiyati,
        decimal KdvOrani,
        decimal Matrah,
        decimal KdvTutari,
        decimal ToplamTutar,
        int? StokLotId,
        int? StokSeriId,
        string? Barkod,
        string StokKodu,
        string UrunAdi,
        string Birim,
        string TakipTipi,
        string? LotNo,
        DateTime? SonKullanmaTarihi,
        string? SeriNo);

    private sealed record OdemeProjection(
        string OdemeYontemi,
        int? KasaBankaHesapId,
        decimal Tutar,
        string? HesapKodSnapshot,
        string? HesapAdSnapshot);
}
