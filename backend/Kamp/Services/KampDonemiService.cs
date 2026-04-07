using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using STYS.Kamp.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampDonemiService : BaseRdbmsService<KampDonemiDto, KampDonemi, int>, IKampDonemiService
{
    private readonly IKampDonemiRepository _kampDonemiRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public KampDonemiService(
        IKampDonemiRepository kampDonemiRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(kampDonemiRepository, mapper)
    {
        _kampDonemiRepository = kampDonemiRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public override async Task<KampDonemiDto> AddAsync(KampDonemiDto dto)
    {
        await EnsureCanManageGlobalAsync();
        await EnsureValidProgramAsync(dto.KampProgramiId);
        Normalize(dto);
        await EnsureUniqueAsync(dto, null);
        return await base.AddAsync(dto);
    }

    public override async Task<KampDonemiDto> UpdateAsync(KampDonemiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Kamp donemi id zorunludur.", 400);
        }

        await EnsureCanManageGlobalAsync();
        await EnsureValidProgramAsync(dto.KampProgramiId);
        Normalize(dto);
        await EnsureUniqueAsync(dto, dto.Id.Value);

        var entity = await _kampDonemiRepository.GetByIdAsync(dto.Id.Value);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek kamp donemi bulunamadi.", 404);
        }

        entity.IsDeleted = false;
        entity.KampProgramiId = dto.KampProgramiId;
        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.Yil = dto.Yil;
        entity.BasvuruBaslangicTarihi = dto.BasvuruBaslangicTarihi.Date;
        entity.BasvuruBitisTarihi = dto.BasvuruBitisTarihi.Date;
        entity.KonaklamaBaslangicTarihi = dto.KonaklamaBaslangicTarihi.Date;
        entity.KonaklamaBitisTarihi = dto.KonaklamaBitisTarihi.Date;
        entity.MinimumGece = dto.MinimumGece;
        entity.MaksimumGece = dto.MaksimumGece;
        entity.OnayGerektirirMi = dto.OnayGerektirirMi;
        entity.CekilisGerekliMi = dto.CekilisGerekliMi;
        entity.AyniAileIcinTekBasvuruMu = dto.AyniAileIcinTekBasvuruMu;
        entity.IptalSonGun = dto.IptalSonGun?.Date;
        entity.AktifMi = dto.AktifMi;

        _kampDonemiRepository.Update(entity);
        await _kampDonemiRepository.SaveChangesAsync();
        return (await GetByIdAsync(entity.Id))!;
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanManageGlobalAsync();
        await base.DeleteAsync(id);
    }

    public override async Task<IEnumerable<KampDonemiDto>> GetAllAsync(Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null)
    {
        return await GetAllAsync(include, CancellationToken.None);
    }

    public async Task<IEnumerable<KampDonemiDto>> GetAllAsync(Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null, CancellationToken cancellationToken = default)
    {
        var query = BuildIncludedQuery(include);
        var items = await query
            .OrderByDescending(x => x.Yil)
            .ThenBy(x => x.KampProgrami!.Ad)
            .ThenBy(x => x.Ad)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<KampDonemiDto>>(items);
    }

    public override async Task<KampDonemiDto?> GetByIdAsync(int id, Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null)
    {
        return await GetByIdAsync(id, include, CancellationToken.None);
    }

    public async Task<KampDonemiDto?> GetByIdAsync(int id, Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null, CancellationToken cancellationToken = default)
    {
        var entity = await BuildIncludedQuery(include)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : Mapper.Map<KampDonemiDto>(entity);
    }

    public override async Task<PagedResult<KampDonemiDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<KampDonemi, bool>>? predicate = null,
        Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null,
        Func<IQueryable<KampDonemi>, IOrderedQueryable<KampDonemi>>? orderBy = null)
    {
        return await GetPagedAsync(request, predicate, include, orderBy, CancellationToken.None);
    }

    public async Task<PagedResult<KampDonemiDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<KampDonemi, bool>>? predicate = null,
        Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null,
        Func<IQueryable<KampDonemi>, IOrderedQueryable<KampDonemi>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildIncludedQuery(include);
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var ordered = orderBy is not null
            ? orderBy(query)
            : query.OrderByDescending(x => x.Yil).ThenBy(x => x.KampProgrami!.Ad).ThenBy(x => x.Ad);

        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KampDonemiDto>(Mapper.Map<List<KampDonemiDto>>(items), pageNumber, pageSize, totalCount);
    }

    public async Task<KampDonemiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var tesisQuery = _stysDbContext.Tesisler.Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            tesisQuery = tesisQuery.Where(x => scope.TesisIds.Contains(x.Id));
        }

        var programlar = await _stysDbContext.KampProgramlari
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new KampProgramiSecenekDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        var tesisler = await tesisQuery
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new KampTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new KampDonemiYonetimBaglamDto
        {
            GlobalDonemYonetimiYapabilirMi = !scope.IsScoped,
            Programlar = programlar,
            Tesisler = tesisler
        };
    }

    public async Task<List<KampDonemiTesisAtamaDto>> GetTesisAtamalariAsync(int kampDonemiId, CancellationToken cancellationToken = default)
    {
        await EnsureKampDonemiExistsAsync(kampDonemiId, cancellationToken);

        var accessibleTesisler = await GetAccessibleTesislerAsync(cancellationToken);
        var accessibleIds = accessibleTesisler.Select(x => x.Id).ToList();

        var existingMap = await _stysDbContext.KampDonemiTesisleri
            .Where(x => x.KampDonemiId == kampDonemiId && accessibleIds.Contains(x.TesisId))
            .ToDictionaryAsync(x => x.TesisId, cancellationToken);

        return accessibleTesisler
            .Select(tesis =>
            {
                existingMap.TryGetValue(tesis.Id, out var existing);
                return new KampDonemiTesisAtamaDto
                {
                    TesisId = tesis.Id,
                    TesisAd = tesis.Ad,
                    AtamaVarMi = existing is not null,
                    DonemdeAktifMi = existing?.AktifMi ?? true,
                    BasvuruyaAcikMi = existing?.BasvuruyaAcikMi ?? true,
                    ToplamKontenjan = existing?.ToplamKontenjan ?? 0,
                    Aciklama = existing?.Aciklama
                };
            })
            .ToList();
    }

    public async Task<List<KampDonemiTesisAtamaDto>> KaydetTesisAtamalariAsync(int kampDonemiId, IReadOnlyCollection<KampDonemiTesisAtamaKayitDto> kayitlar, CancellationToken cancellationToken = default)
    {
        await EnsureKampDonemiExistsAsync(kampDonemiId, cancellationToken);

        var accessibleTesisler = await GetAccessibleTesislerAsync(cancellationToken);
        var accessibleIds = accessibleTesisler.Select(x => x.Id).ToHashSet();

        var normalizedKayitlar = (kayitlar ?? [])
            .GroupBy(x => x.TesisId)
            .Select(group => group.Last())
            .ToList();

        if (normalizedKayitlar.Any(x => !accessibleIds.Contains(x.TesisId)))
        {
            throw new BaseException("Erisim yetkiniz olmayan bir tesis icin atama kaydi gonderildi.", 403);
        }

        foreach (var kayit in normalizedKayitlar.Where(x => x.AtamaVarMi))
        {
            if (kayit.ToplamKontenjan <= 0)
            {
                throw new BaseException("Atamasi yapilan tesisler icin toplam kontenjan sifirdan buyuk olmalidir.", 400);
            }
        }

        var existingAssignments = await _stysDbContext.KampDonemiTesisleri
            .Where(x => x.KampDonemiId == kampDonemiId && accessibleIds.Contains(x.TesisId))
            .ToListAsync(cancellationToken);

        var existingByTesisId = existingAssignments.ToDictionary(x => x.TesisId);
        var requestedByTesisId = normalizedKayitlar.ToDictionary(x => x.TesisId);

        foreach (var accessibleTesisId in accessibleIds)
        {
            var hasRequest = requestedByTesisId.TryGetValue(accessibleTesisId, out var kayit);
            var shouldBeAssigned = hasRequest && kayit!.AtamaVarMi;

            if (existingByTesisId.TryGetValue(accessibleTesisId, out var existing))
            {
                if (!shouldBeAssigned)
                {
                    _stysDbContext.KampDonemiTesisleri.Remove(existing);
                    continue;
                }

                existing.IsDeleted = false;
                existing.AktifMi = kayit!.DonemdeAktifMi;
                existing.BasvuruyaAcikMi = kayit.BasvuruyaAcikMi;
                existing.ToplamKontenjan = kayit.ToplamKontenjan;
                existing.Aciklama = NormalizeAciklama(kayit.Aciklama);
                continue;
            }

            if (!shouldBeAssigned)
            {
                continue;
            }

            await _stysDbContext.KampDonemiTesisleri.AddAsync(new KampDonemiTesis
            {
                KampDonemiId = kampDonemiId,
                TesisId = kayit!.TesisId,
                AktifMi = kayit.DonemdeAktifMi,
                BasvuruyaAcikMi = kayit.BasvuruyaAcikMi,
                ToplamKontenjan = kayit.ToplamKontenjan,
                Aciklama = NormalizeAciklama(kayit.Aciklama)
            }, cancellationToken);
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return await GetTesisAtamalariAsync(kampDonemiId, cancellationToken);
    }

    private IQueryable<KampDonemi> BuildIncludedQuery(Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null)
    {
        var query = _stysDbContext.KampDonemleri
            .Include(x => x.KampProgrami)
            .AsQueryable();

        return include is null ? query : include(query);
    }

    private async Task EnsureCanManageGlobalAsync()
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            throw new BaseException("Kamp donemi tanimlari yalnizca merkez yoneticileri tarafindan yonetilebilir.", 403);
        }
    }

    private async Task EnsureValidProgramAsync(int kampProgramiId)
    {
        if (kampProgramiId <= 0)
        {
            throw new BaseException("Kamp programi secimi zorunludur.", 400);
        }

        var exists = await _stysDbContext.KampProgramlari.AnyAsync(x => x.Id == kampProgramiId);
        if (!exists)
        {
            throw new BaseException("Secilen kamp programi bulunamadi.", 404);
        }
    }

    private async Task EnsureUniqueAsync(KampDonemiDto dto, int? excludedId)
    {
        var normalizedKod = dto.Kod.Trim().ToUpperInvariant();
        var normalizedAd = dto.Ad.Trim().ToUpperInvariant();

        var kodExists = await _stysDbContext.KampDonemleri.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId.Value)
            && x.Kod.ToUpper() == normalizedKod);

        if (kodExists)
        {
            throw new BaseException("Ayni kod ile baska bir kamp donemi zaten mevcut.", 400);
        }

        var adExists = await _stysDbContext.KampDonemleri.AnyAsync(x =>
            (!excludedId.HasValue || x.Id != excludedId.Value)
            && x.KampProgramiId == dto.KampProgramiId
            && x.Yil == dto.Yil
            && x.Ad.ToUpper() == normalizedAd);

        if (adExists)
        {
            throw new BaseException("Ayni program ve yil altinda ayni ada sahip baska bir kamp donemi zaten mevcut.", 400);
        }
    }

    private async Task EnsureKampDonemiExistsAsync(int kampDonemiId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.KampDonemleri.AnyAsync(x => x.Id == kampDonemiId, cancellationToken);
        if (!exists)
        {
            throw new BaseException("Kamp donemi bulunamadi.", 404);
        }
    }

    private async Task<List<KampTesisDto>> GetAccessibleTesislerAsync(CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler.Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new KampTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    private static string? NormalizeAciklama(string? aciklama)
        => string.IsNullOrWhiteSpace(aciklama) ? null : aciklama.Trim();

    private static void Normalize(KampDonemiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Kamp donemi kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Kamp donemi adi zorunludur.", 400);
        }

        if (dto.Yil < KampValidasyonKurallari.YilRange.Min || dto.Yil > KampValidasyonKurallari.YilRange.Max)
        {
            throw new BaseException("Kamp donemi yili gecersiz.", 400);
        }

        if (dto.BasvuruBaslangicTarihi.Date > dto.BasvuruBitisTarihi.Date)
        {
            throw new BaseException("Basvuru baslangic tarihi basvuru bitis tarihinden buyuk olamaz.", 400);
        }

        if (dto.KonaklamaBaslangicTarihi.Date > dto.KonaklamaBitisTarihi.Date)
        {
            throw new BaseException("Konaklama baslangic tarihi konaklama bitis tarihinden buyuk olamaz.", 400);
        }

        if (dto.MinimumGece <= 0)
        {
            throw new BaseException("Minimum gece en az 1 olmalidir.", 400);
        }

        if (dto.MaksimumGece < dto.MinimumGece)
        {
            throw new BaseException("Maksimum gece minimum geceden kucuk olamaz.", 400);
        }

        if (dto.IptalSonGun.HasValue && dto.IptalSonGun.Value.Date > dto.KonaklamaBaslangicTarihi.Date)
        {
            throw new BaseException("Iptal son gunu konaklama baslangic tarihinden sonra olamaz.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.BasvuruBaslangicTarihi = dto.BasvuruBaslangicTarihi.Date;
        dto.BasvuruBitisTarihi = dto.BasvuruBitisTarihi.Date;
        dto.KonaklamaBaslangicTarihi = dto.KonaklamaBaslangicTarihi.Date;
        dto.KonaklamaBitisTarihi = dto.KonaklamaBitisTarihi.Date;
        dto.IptalSonGun = dto.IptalSonGun?.Date;
    }
}
