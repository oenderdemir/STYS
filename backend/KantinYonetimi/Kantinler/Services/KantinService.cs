using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.KantinYonetimi.Kantinler.Dtos;
using STYS.KantinYonetimi.Kantinler.Entities;
using STYS.KantinYonetimi.Kantinler.Repositories;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.StokHareketleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.KantinYonetimi.Kantinler.Services;

public class KantinService : BaseRdbmsService<KantinDto, Kantin, int>, IKantinService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly IStokHareketRepository _stokHareketRepository;
    private readonly IKantinRepository _repository;
    private readonly IKantinUrunRepository _kantinUrunRepository;
    private readonly IMapper _mapper;

    public KantinService(
        StysAppDbContext dbContext,
        IUserAccessScopeService userAccessScopeService,
        IStokHareketRepository stokHareketRepository,
        IKantinRepository repository,
        IKantinUrunRepository kantinUrunRepository,
        IMapper mapper)
        : base(repository, mapper)
    {
        _dbContext = dbContext;
        _userAccessScopeService = userAccessScopeService;
        _stokHareketRepository = stokHareketRepository;
        _repository = repository;
        _kantinUrunRepository = kantinUrunRepository;
        _mapper = mapper;
    }

    public async Task<List<KantinDto>> GetListAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = BuildScopedKantinQuery(scope);
        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        return await query
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .Select(x => MapKantinDto(x))
            .ToListAsync(cancellationToken);
    }

    public Task<KantinDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, include: null);

    public override async Task<KantinDto?> GetByIdAsync(int id, Func<IQueryable<Kantin>, IQueryable<Kantin>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        return await BuildScopedKantinQuery(scope)
            .Where(x => x.Id == id)
            .Select(x => MapKantinDto(x))
            .FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<KantinDto>> GetAllAsync(Func<IQueryable<Kantin>, IQueryable<Kantin>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedKantinQuery(scope)
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .ToListAsync();

        return items.Select(MapKantinDto).ToList();
    }

    public override async Task<IEnumerable<KantinDto>> WhereAsync(System.Linq.Expressions.Expression<Func<Kantin, bool>> predicate, Func<IQueryable<Kantin>, IQueryable<Kantin>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var items = await BuildScopedKantinQuery(scope)
            .Where(predicate)
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .ToListAsync();

        return items.Select(MapKantinDto).ToList();
    }

    public override Task<KantinDto> AddAsync(KantinDto dto)
        => AddCoreAsync(dto);

    public Task<KantinDto> AddAsync(KantinDto dto, CancellationToken cancellationToken = default)
        => AddCoreAsync(dto);

    public override Task<KantinDto> UpdateAsync(KantinDto dto)
        => UpdateCoreAsync(dto);

    public Task<KantinDto> UpdateAsync(KantinDto dto, CancellationToken cancellationToken = default)
        => UpdateCoreAsync(dto);

    private async Task<KantinDto> AddCoreAsync(KantinDto dto)
    {
        await EnsureTesisAccessAsync(dto.TesisId, CancellationToken.None);
        NormalizeKantinDto(dto);
        await ValidateKantinAsync(dto, null, CancellationToken.None);
        var result = await base.AddAsync(dto);
        return await GetByIdAsync(result.Id!.Value) ?? result;
    }

    private async Task<KantinDto> UpdateCoreAsync(KantinDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kantin id zorunludur.", 400);
        }

        var entity = await _repository.GetByIdAsync(dto.Id.Value)
            ?? throw new BaseException("Kantin bulunamadı.", 404);

        await EnsureTesisAccessAsync(entity.TesisId, CancellationToken.None);
        dto.TesisId = entity.TesisId;
        NormalizeKantinDto(dto);
        await ValidateKantinAsync(dto, entity.Id, CancellationToken.None);

        var result = await base.UpdateAsync(dto);
        return await GetByIdAsync(result.Id!.Value) ?? result;
    }

    public async Task<List<KantinUrunDto>> GetUrunlerAsync(int kantinId, CancellationToken cancellationToken = default)
    {
        var kantin = await GetRequiredKantinAsync(kantinId, cancellationToken);
        var bakiyeler = await _stokHareketRepository.GetDepoStokBakiyeleriAsync([kantin.DepoId], cancellationToken);
        var bakiyeMap = bakiyeler
            .Where(x => x.DepoId == kantin.DepoId)
            .GroupBy(x => x.TasinirKartId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.BakiyeMiktari));

        var urunler = await _dbContext.KantinUrunler
            .AsNoTracking()
            .Include(x => x.TasinirKart)
            .Where(x => x.KantinId == kantinId && !x.IsDeleted)
            .OrderBy(x => x.SiraNo ?? int.MaxValue)
            .ThenBy(x => x.TasinirKart!.StokKodu)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return urunler.Select(x => new KantinUrunDto
        {
            Id = x.Id,
            KantinId = x.KantinId,
            TasinirKartId = x.TasinirKartId,
            Barkod = x.Barkod,
            SatisFiyati = x.SatisFiyati,
            AktifMi = x.AktifMi,
            SiraNo = x.SiraNo,
            Aciklama = x.Aciklama,
            StokKodu = x.TasinirKart?.StokKodu,
            UrunAdi = x.TasinirKart?.Ad,
            Birim = x.TasinirKart?.Birim,
            KdvOrani = x.TasinirKart?.KdvOrani ?? 0,
            MevcutStok = bakiyeMap.TryGetValue(x.TasinirKartId, out var bakiye) ? bakiye : 0,
            TakipTipi = x.TasinirKart is null ? null : ResolveTakipTipi(x.TasinirKart.TakipTipi, x.TasinirKart.TakipliMi)
        }).ToList();
    }

    public async Task<KantinUrunDto> AddUrunAsync(int kantinId, KantinUrunDto dto, CancellationToken cancellationToken = default)
    {
        var kantin = await GetRequiredKantinAsync(kantinId, cancellationToken);
        dto.KantinId = kantin.Id;
        await ValidateKantinUrunAsync(kantin, dto, null, cancellationToken);

        var entity = new KantinUrun
        {
            KantinId = kantin.Id,
            TasinirKartId = dto.TasinirKartId,
            Barkod = NormalizeBarcode(dto.Barkod),
            SatisFiyati = dto.SatisFiyati,
            AktifMi = dto.AktifMi,
            SiraNo = dto.SiraNo,
            Aciklama = NormalizeOptional(dto.Aciklama, 1024)
        };

        await _kantinUrunRepository.AddAsync(entity);
        await _kantinUrunRepository.SaveChangesAsync();
        return await GetRequiredUrunDtoAsync(kantin.Id, entity.Id, cancellationToken);
    }

    public async Task<KantinUrunDto> UpdateUrunAsync(int kantinId, KantinUrunDto dto, CancellationToken cancellationToken = default)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kantin ürün id zorunludur.", 400);
        }

        var kantin = await GetRequiredKantinAsync(kantinId, cancellationToken);
        var entity = await _dbContext.KantinUrunler
            .FirstOrDefaultAsync(x => x.Id == dto.Id.Value && x.KantinId == kantinId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin ürünü bulunamadı.", 404);

        dto.KantinId = kantin.Id;
        await ValidateKantinUrunAsync(kantin, dto, entity.Id, cancellationToken);

        entity.TasinirKartId = dto.TasinirKartId;
        entity.Barkod = NormalizeBarcode(dto.Barkod);
        entity.SatisFiyati = dto.SatisFiyati;
        entity.AktifMi = dto.AktifMi;
        entity.SiraNo = dto.SiraNo;
        entity.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        _kantinUrunRepository.Update(entity);
        await _kantinUrunRepository.SaveChangesAsync();
        return await GetRequiredUrunDtoAsync(kantin.Id, entity.Id, cancellationToken);
    }

    public async Task<List<KantinDepoSecenekDto>> GetDepolarAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureTesisAccessAsync(tesisId, cancellationToken);
        return await _dbContext.Depolar
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == tesisId && x.AktifMi)
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .Select(x => new KantinDepoSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KantinKasaSecenekDto>> GetNakitKasalarAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureTesisAccessAsync(tesisId, cancellationToken);
        return await _dbContext.KasaBankaHesaplari
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == tesisId && x.AktifMi && x.Tip == KasaBankaHesapTipleri.NakitKasa)
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .Select(x => new KantinKasaSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KantinCariKartSecenekDto>> GetPerakendeCariKartlarAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureTesisAccessAsync(tesisId, cancellationToken);
        return await _dbContext.CariKartlar
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.TesisId == tesisId &&
                x.AktifMi &&
                (x.CariTipi == CariKartTipleri.Musteri || x.CariTipi == CariKartTipleri.KurumsalMusteri))
            .OrderBy(x => x.CariKodu)
            .ThenBy(x => x.UnvanAdSoyad)
            .Select(x => new KantinCariKartSecenekDto
            {
                Id = x.Id,
                CariKodu = x.CariKodu,
                UnvanAdSoyad = x.UnvanAdSoyad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KantinOdemeHesapSecenekDto>> GetOdemeHesaplariAsync(int tesisId, string odemeYontemi, CancellationToken cancellationToken = default)
    {
        await EnsureTesisAccessAsync(tesisId, cancellationToken);
        var normalizedYontem = NormalizeRequired(odemeYontemi, "Ödeme yöntemi zorunludur.", 32);
        var hesapTipi = string.Equals(normalizedYontem, STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities.OdemeYontemleri.KrediKarti, StringComparison.Ordinal)
            ? KasaBankaHesapTipleri.KrediKarti
            : KasaBankaHesapTipleri.NakitKasa;

        return await _dbContext.KasaBankaHesaplari
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == tesisId && x.AktifMi && x.Tip == hesapTipi)
            .OrderBy(x => x.Kod)
            .ThenBy(x => x.Ad)
            .Select(x => new KantinOdemeHesapSecenekDto
            {
                Id = x.Id,
                Kod = x.Kod,
                Ad = x.Ad,
                Tip = x.Tip
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KantinTasinirKartSecenekDto>> GetTasinirKartlarAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureTesisAccessAsync(tesisId, cancellationToken);
        return await _dbContext.TasinirKartlar
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.TesisId == tesisId && x.AktifMi)
            .OrderBy(x => x.StokKodu)
            .ThenBy(x => x.Ad)
            .Select(x => new KantinTasinirKartSecenekDto
            {
                Id = x.Id,
                StokKodu = x.StokKodu,
                Ad = x.Ad,
                Birim = x.Birim,
                KdvOrani = x.KdvOrani
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Kantin> BuildScopedKantinQuery(DomainAccessScope scope)
    {
        var query = _dbContext.Kantinler
            .AsNoTracking()
            .Include(x => x.Depo)
            .Include(x => x.VarsayilanNakitKasa)
            .Include(x => x.VarsayilanPosHesap)
            .Include(x => x.PerakendeCariKart)
            .Where(x => !x.IsDeleted);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.TesisId));
        }

        return query;
    }

    private async Task<Kantin> GetRequiredKantinAsync(int kantinId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Kantinler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == kantinId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kantin bulunamadı.", 404);

        await EnsureTesisAccessAsync(entity.TesisId, cancellationToken);
        return entity;
    }

    private async Task<KantinUrunDto> GetRequiredUrunDtoAsync(int kantinId, int urunId, CancellationToken cancellationToken)
        => (await GetUrunlerAsync(kantinId, cancellationToken)).FirstOrDefault(x => x.Id == urunId)
            ?? throw new BaseException("Kantin ürünü bulunamadı.", 404);

    private async Task ValidateKantinAsync(KantinDto dto, int? excludedId, CancellationToken cancellationToken)
    {
        var depo = await _dbContext.Depolar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.DepoId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Seçilen depo bulunamadı.", 400);

        if (!depo.TesisId.HasValue || depo.TesisId.Value != dto.TesisId)
        {
            throw new BaseException("Seçilen depo kantin ile aynı tesise ait olmalıdır.", 400);
        }

        if (dto.VarsayilanNakitKasaId.HasValue)
        {
            var kasa = await _dbContext.KasaBankaHesaplari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.VarsayilanNakitKasaId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Seçilen varsayılan kasa bulunamadı.", 400);

            if (kasa.TesisId != dto.TesisId)
            {
                throw new BaseException("Seçilen varsayılan kasa kantin ile aynı tesise ait olmalıdır.", 400);
            }

            if (!kasa.AktifMi)
            {
                throw new BaseException("Seçilen varsayılan kasa aktif olmalıdır.", 400);
            }

            if (!string.Equals(kasa.Tip, KasaBankaHesapTipleri.NakitKasa, StringComparison.Ordinal))
            {
                throw new BaseException("Varsayılan kasa yalnızca nakit kasa tipinde olabilir.", 400);
            }
        }

        if (dto.VarsayilanPosHesapId.HasValue)
        {
            var posHesap = await _dbContext.KasaBankaHesaplari
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.VarsayilanPosHesapId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Seçilen varsayılan POS hesabı bulunamadı.", 400);

            if (posHesap.TesisId != dto.TesisId)
            {
                throw new BaseException("Seçilen varsayılan POS hesabı kantin ile aynı tesise ait olmalıdır.", 400);
            }

            if (!posHesap.AktifMi)
            {
                throw new BaseException("Seçilen varsayılan POS hesabı aktif olmalıdır.", 400);
            }

            if (!string.Equals(posHesap.Tip, KasaBankaHesapTipleri.KrediKarti, StringComparison.Ordinal))
            {
                throw new BaseException("Varsayılan POS hesabı yalnızca kredi kartı tipinde olabilir.", 400);
            }
        }

        if (dto.PerakendeCariKartId.HasValue)
        {
            var cari = await _dbContext.CariKartlar
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.PerakendeCariKartId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new BaseException("Seçilen perakende cari bulunamadı.", 400);

            if (cari.TesisId != dto.TesisId)
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

        var normalizedKod = NormalizeRequired(dto.Kod, "Kantin kodu zorunludur.", 64).ToUpperInvariant();
        var duplicateKod = await _dbContext.Kantinler
            .AsNoTracking()
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.TesisId == dto.TesisId &&
                x.Id != excludedId &&
                x.Kod.ToUpper() == normalizedKod,
                cancellationToken);

        if (duplicateKod)
        {
            throw new BaseException("Aynı tesis içinde bu kantin kodu zaten kullanılıyor.", 400);
        }
    }

    private async Task ValidateKantinUrunAsync(Kantin kantin, KantinUrunDto dto, int? excludedId, CancellationToken cancellationToken)
    {
        if (dto.SatisFiyati < 0)
        {
            throw new BaseException("Satış fiyatı negatif olamaz.", 400);
        }

        var kart = await _dbContext.TasinirKartlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dto.TasinirKartId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Seçilen taşınır kart bulunamadı.", 400);

        if (!kart.TesisId.HasValue || kart.TesisId.Value != kantin.TesisId)
        {
            throw new BaseException("Seçilen taşınır kart kantin ile aynı tesise ait olmalıdır.", 400);
        }

        if (!kart.AktifMi)
        {
            throw new BaseException("Seçilen taşınır kart aktif olmalıdır.", 400);
        }

        var duplicateKart = await _dbContext.KantinUrunler
            .AsNoTracking()
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.KantinId == kantin.Id &&
                x.Id != excludedId &&
                x.TasinirKartId == dto.TasinirKartId,
                cancellationToken);

        if (duplicateKart)
        {
            throw new BaseException("Aynı taşınır kart aynı kantine birden fazla eklenemez.", 400);
        }

        var barkod = NormalizeBarcode(dto.Barkod);
        if (!string.IsNullOrWhiteSpace(barkod))
        {
            var duplicateBarkod = await _dbContext.KantinUrunler
                .AsNoTracking()
                .AnyAsync(x =>
                    !x.IsDeleted &&
                    x.KantinId == kantin.Id &&
                    x.Id != excludedId &&
                    x.Barkod == barkod,
                    cancellationToken);

            if (duplicateBarkod)
            {
                throw new BaseException("Aynı kantin içinde bu barkod zaten kullanılıyor.", 400);
            }
        }
    }

    private async Task EnsureTesisAccessAsync(int tesisId, CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis için yetkiniz bulunmuyor.", 403);
        }
    }

    private static KantinDto MapKantinDto(Kantin entity)
        => new()
        {
            Id = entity.Id,
            TesisId = entity.TesisId,
            DepoId = entity.DepoId,
            VarsayilanNakitKasaId = entity.VarsayilanNakitKasaId,
            VarsayilanPosHesapId = entity.VarsayilanPosHesapId,
            PerakendeCariKartId = entity.PerakendeCariKartId,
            Kod = entity.Kod,
            Ad = entity.Ad,
            AktifMi = entity.AktifMi,
            Aciklama = entity.Aciklama,
            DepoKod = entity.Depo?.Kod,
            DepoAd = entity.Depo?.Ad,
            VarsayilanNakitKasaAd = entity.VarsayilanNakitKasa?.Ad,
            VarsayilanPosHesapAd = entity.VarsayilanPosHesap?.Ad,
            PerakendeCariKartAd = entity.PerakendeCariKart is null
                ? null
                : $"{entity.PerakendeCariKart.CariKodu} - {entity.PerakendeCariKart.UnvanAdSoyad}"
        };

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

    private static string ResolveTakipTipi(string? takipTipi, bool takipliMi)
        => !string.IsNullOrWhiteSpace(takipTipi)
            ? takipTipi
            : takipliMi
                ? STYS.Muhasebe.TasinirKartlari.Entities.TasinirKartTakipTipleri.Lot
                : STYS.Muhasebe.TasinirKartlari.Entities.TasinirKartTakipTipleri.Yok;

    private static void NormalizeKantinDto(KantinDto dto)
    {
        dto.Kod = NormalizeRequired(dto.Kod, "Kantin kodu zorunludur.", 64).ToUpperInvariant();
        dto.Ad = NormalizeRequired(dto.Ad, "Kantin adı zorunludur.", 200);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);
    }
}
