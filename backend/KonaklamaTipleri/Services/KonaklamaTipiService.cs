using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.KonaklamaTipleri;
using STYS.KonaklamaTipleri.Dto;
using STYS.KonaklamaTipleri.Entities;
using STYS.KonaklamaTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;
using System.Globalization;

namespace STYS.KonaklamaTipleri.Services;

public class KonaklamaTipiService : BaseRdbmsService<KonaklamaTipiDto, KonaklamaTipi, int>, IKonaklamaTipiService
{
    private readonly IKonaklamaTipiRepository _konaklamaTipiRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public KonaklamaTipiService(
        IKonaklamaTipiRepository konaklamaTipiRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(konaklamaTipiRepository, mapper)
    {
        _konaklamaTipiRepository = konaklamaTipiRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public override async Task<KonaklamaTipiDto> AddAsync(KonaklamaTipiDto dto)
    {
        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, null);
        ValidateIcerikKalemleri(dto);

        var entity = Mapper.Map<KonaklamaTipi>(dto);
        entity.IcerikKalemleri = CreateIcerikEntities(dto);

        await _konaklamaTipiRepository.AddAsync(entity);
        await _konaklamaTipiRepository.SaveChangesAsync();

        var saved = await GetEntityWithIcerikAsync(entity.Id);
        return MapDto(saved ?? entity);
    }

    public override async Task<KonaklamaTipiDto> UpdateAsync(KonaklamaTipiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Konaklama tipi id zorunludur.", 400);
        }

        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, dto.Id.Value);
        ValidateIcerikKalemleri(dto);

        var entity = await GetEntityWithIcerikAsync(dto.Id.Value);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek konaklama tipi bulunamadi.", 404);
        }

        entity.IsDeleted = false;
        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.AktifMi = dto.AktifMi;
        SyncIcerikKalemleri(entity, dto);

        _konaklamaTipiRepository.Update(entity);
        await _konaklamaTipiRepository.SaveChangesAsync();

        var saved = await GetEntityWithIcerikAsync(entity.Id);
        return MapDto(saved ?? entity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanManageGlobalAsync();
        await base.DeleteAsync(id);
    }

    public async Task<KonaklamaTipiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler
            .Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        var tesisler = await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new KonaklamaTipiTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new KonaklamaTipiYonetimBaglamDto
        {
            GlobalTipYonetimiYapabilirMi = !scope.IsScoped,
            Tesisler = tesisler
        };
    }

    public async Task<List<KonaklamaTipiTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var seciliTipIds = await _stysDbContext.TesisKonaklamaTipleri
            .Where(x => x.TesisId == tesisId && x.AktifMi && !x.IsDeleted)
            .Select(x => x.KonaklamaTipiId)
            .ToListAsync(cancellationToken);

        var seciliSet = seciliTipIds.ToHashSet();
        var globalCounts = await _stysDbContext.KonaklamaTipiIcerikKalemleri
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.KonaklamaTipiId)
            .Select(group => new
            {
                KonaklamaTipiId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(x => x.KonaklamaTipiId, x => x.Count, cancellationToken);

        var overrideInfos = await _stysDbContext.Set<TesisKonaklamaTipiIcerikOverride>()
            .Where(x => x.TesisId == tesisId
                && !x.IsDeleted
                && x.KonaklamaTipiIcerikKalemi != null
                && !x.KonaklamaTipiIcerikKalemi.IsDeleted)
            .GroupBy(x => x.KonaklamaTipiIcerikKalemi!.KonaklamaTipiId)
            .Select(group => new
            {
                KonaklamaTipiId = group.Key,
                OverrideVarMi = group.Any(),
                DevreDisiSayisi = group.Count(x => x.DevreDisiMi)
            })
            .ToDictionaryAsync(x => x.KonaklamaTipiId, x => new { x.OverrideVarMi, x.DevreDisiSayisi }, cancellationToken);

        var tipler = await _stysDbContext.KonaklamaTipleri
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return tipler.Select(x => new KonaklamaTipiTesisAtamaDto
        {
            KonaklamaTipiId = x.Id,
            Kod = x.Kod,
            Ad = x.Ad,
            GlobalAktifMi = x.AktifMi,
            TesisteKullanilabilirMi = seciliSet.Contains(x.Id),
            OverrideVarMi = overrideInfos.ContainsKey(x.Id) && overrideInfos[x.Id].OverrideVarMi,
            EtkinIcerikSayisi = Math.Max(0, globalCounts.GetValueOrDefault(x.Id) - (overrideInfos.ContainsKey(x.Id) ? overrideInfos[x.Id].DevreDisiSayisi : 0))
        }).ToList();
    }

    public async Task<List<KonaklamaTipiTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> konaklamaTipiIds, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var hedefIdler = (konaklamaTipiIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var hedefTipler = await _stysDbContext.KonaklamaTipleri
            .Where(x => hedefIdler.Contains(x.Id) && x.AktifMi)
            .Select(x => new { x.Id, x.Ad })
            .ToListAsync(cancellationToken);

        if (hedefTipler.Count != hedefIdler.Count)
        {
            throw new BaseException("Secilen konaklama tiplerinden biri gecersiz veya pasif.", 400);
        }

        var mevcutlar = await _stysDbContext.TesisKonaklamaTipleri
            .Where(x => x.TesisId == tesisId)
            .ToListAsync(cancellationToken);

        var kaldirilacakIdler = mevcutlar
            .Where(x => x.AktifMi && !x.IsDeleted && !hedefIdler.Contains(x.KonaklamaTipiId))
            .Select(x => x.KonaklamaTipiId)
            .Distinct()
            .ToList();

        if (kaldirilacakIdler.Count > 0)
        {
            var kilitliIdler = await GetKilitliKonaklamaTipiIdsAsync(tesisId, kaldirilacakIdler, cancellationToken);
            if (kilitliIdler.Count > 0)
            {
                var kilitliAdlar = await _stysDbContext.KonaklamaTipleri
                    .Where(x => kilitliIdler.Contains(x.Id))
                    .OrderBy(x => x.Ad)
                    .Select(x => x.Ad)
                    .ToListAsync(cancellationToken);

                throw new BaseException(
                    $"Su konaklama tipleri ilgili tesiste fiyat, indirim veya rezervasyon kaydinda kullanildigi icin kaldirilamaz: {string.Join(", ", kilitliAdlar)}.",
                    400);
            }
        }

        var mevcutByTipId = mevcutlar.ToDictionary(x => x.KonaklamaTipiId);
        foreach (var tipId in hedefIdler)
        {
            if (mevcutByTipId.TryGetValue(tipId, out var mevcut))
            {
                mevcut.IsDeleted = false;
                mevcut.AktifMi = true;
                continue;
            }

            await _stysDbContext.TesisKonaklamaTipleri.AddAsync(new TesisKonaklamaTipi
            {
                TesisId = tesisId,
                KonaklamaTipiId = tipId,
                AktifMi = true
            }, cancellationToken);
        }

        foreach (var mevcut in mevcutlar.Where(x => !hedefIdler.Contains(x.KonaklamaTipiId)))
        {
            mevcut.AktifMi = false;
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return await GetTesisAtamalariAsync(tesisId, cancellationToken);
    }

    public async Task<List<KonaklamaTipiTesisIcerikOverrideDto>> GetTesisIcerikOverrideAsync(int tesisId, int konaklamaTipiId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(tesisId, konaklamaTipiId, cancellationToken);

        var globalItems = await _stysDbContext.KonaklamaTipiIcerikKalemleri
            .Where(x => x.KonaklamaTipiId == konaklamaTipiId && !x.IsDeleted)
            .OrderBy(x => x.HizmetKodu)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var overrideMap = await GetTesisOverrideMapAsync(tesisId, globalItems.Select(x => x.Id).ToList(), cancellationToken);
        return globalItems.Select(x => BuildTesisOverrideDto(x, overrideMap.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<List<KonaklamaTipiTesisIcerikOverrideDto>> KaydetTesisIcerikOverrideAsync(int tesisId, int konaklamaTipiId, IReadOnlyCollection<KonaklamaTipiTesisIcerikOverrideDto> items, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        await EnsureTesisHasKonaklamaTipiAsync(tesisId, konaklamaTipiId, cancellationToken);

        var globalItems = await _stysDbContext.KonaklamaTipiIcerikKalemleri
            .Where(x => x.KonaklamaTipiId == konaklamaTipiId && !x.IsDeleted)
            .OrderBy(x => x.HizmetKodu)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var globalById = globalItems.ToDictionary(x => x.Id);
        var requestItems = (items ?? []).ToDictionary(x => x.KonaklamaTipiIcerikKalemiId);

        if (requestItems.Keys.Except(globalById.Keys).Any())
        {
            throw new BaseException("Gecersiz icerik override kaydi gonderildi.", 400);
        }

        var existingOverrides = await _stysDbContext.Set<TesisKonaklamaTipiIcerikOverride>()
            .Where(x => x.TesisId == tesisId
                && !x.IsDeleted
                && x.KonaklamaTipiIcerikKalemi != null
                && x.KonaklamaTipiIcerikKalemi.KonaklamaTipiId == konaklamaTipiId)
            .ToListAsync(cancellationToken);
        var existingByItemId = existingOverrides.ToDictionary(x => x.KonaklamaTipiIcerikKalemiId);

        foreach (var globalItem in globalItems)
        {
            requestItems.TryGetValue(globalItem.Id, out var requestItem);
            if (requestItem is null)
            {
                if (existingByItemId.TryGetValue(globalItem.Id, out var existingToDelete))
                {
                    _stysDbContext.Remove(existingToDelete);
                }

                continue;
            }

            if (!string.Equals(requestItem.HizmetKodu?.Trim(), globalItem.HizmetKodu, StringComparison.OrdinalIgnoreCase))
            {
                throw new BaseException("Override kaydindaki hizmet bilgisi global tanim ile uyusmuyor.", 400);
            }

            ValidateOverrideItem(requestItem);
            var normalized = BuildNormalizedOverride(tesisId, globalItem, requestItem);

            if (!HasMeaningfulOverride(normalized))
            {
                if (existingByItemId.TryGetValue(globalItem.Id, out var existingToDelete))
                {
                    _stysDbContext.Remove(existingToDelete);
                }

                continue;
            }

            if (existingByItemId.TryGetValue(globalItem.Id, out var existing))
            {
                existing.DevreDisiMi = normalized.DevreDisiMi;
                existing.Miktar = normalized.Miktar;
                existing.Periyot = normalized.Periyot;
                existing.KullanimTipi = normalized.KullanimTipi;
                existing.KullanimNoktasi = normalized.KullanimNoktasi;
                existing.KullanimBaslangicSaati = normalized.KullanimBaslangicSaati;
                existing.KullanimBitisSaati = normalized.KullanimBitisSaati;
                existing.CheckInGunuGecerliMi = normalized.CheckInGunuGecerliMi;
                existing.CheckOutGunuGecerliMi = normalized.CheckOutGunuGecerliMi;
                existing.Aciklama = normalized.Aciklama;
                existing.IsDeleted = false;
                continue;
            }

            await _stysDbContext.AddAsync(normalized, cancellationToken);
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return await GetTesisIcerikOverrideAsync(tesisId, konaklamaTipiId, cancellationToken);
    }

    public async Task<List<KonaklamaTipiDto>> GetAktifKonaklamaTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _stysDbContext.KonaklamaTipleri
            .Include(x => x.IcerikKalemleri.Where(y => !y.IsDeleted))
            .Where(x => x.AktifMi
                && _stysDbContext.TesisKonaklamaTipleri.Any(y =>
                    y.TesisId == tesisId
                    && y.KonaklamaTipiId == x.Id
                    && y.AktifMi
                    && !y.IsDeleted))
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var overrideMap = await GetTesisOverrideMapAsync(
            tesisId,
            items.SelectMany(x => x.IcerikKalemleri).Select(x => x.Id).ToList(),
            cancellationToken);

        return items.Select(x =>
        {
            var dto = MapDto(x);
            dto.IcerikKalemleri = x.IcerikKalemleri
                .Where(y => !y.IsDeleted)
                .OrderBy(y => y.HizmetKodu)
                .ThenBy(y => y.Id)
                .Select(y => BuildEffectiveIcerikDto(y, overrideMap.GetValueOrDefault(y.Id)))
                .Where(y => y is not null)
                .Cast<KonaklamaTipiIcerikDto>()
                .ToList();
            return dto;
        }).ToList();
    }

    public override async Task<PagedResult<KonaklamaTipiDto>> GetPagedAsync(
        PagedRequest request,
        System.Linq.Expressions.Expression<Func<KonaklamaTipi, bool>>? predicate = null,
        Func<IQueryable<KonaklamaTipi>, IQueryable<KonaklamaTipi>>? include = null,
        Func<IQueryable<KonaklamaTipi>, IOrderedQueryable<KonaklamaTipi>>? orderBy = null)
    {
        var pagedEntities = await _konaklamaTipiRepository.GetPagedAsync(request, predicate, include ?? WithIcerik, orderBy);
        var mappedItems = pagedEntities.Items.Select(MapDto).ToList();
        return new PagedResult<KonaklamaTipiDto>(mappedItems, pagedEntities.PageNumber, pagedEntities.PageSize, pagedEntities.TotalCount);
    }

    public override async Task<IEnumerable<KonaklamaTipiDto>> GetAllAsync(Func<IQueryable<KonaklamaTipi>, IQueryable<KonaklamaTipi>>? include = null)
    {
        var items = await _konaklamaTipiRepository.GetAllAsync(include ?? WithIcerik);
        return items.Select(MapDto).ToList();
    }

    public override async Task<KonaklamaTipiDto?> GetByIdAsync(int id, Func<IQueryable<KonaklamaTipi>, IQueryable<KonaklamaTipi>>? include = null)
    {
        var entity = await _konaklamaTipiRepository.GetByIdAsync(id, include ?? WithIcerik);
        return entity is null ? null : MapDto(entity);
    }

    private async Task EnsureCanManageGlobalAsync()
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            throw new BaseException("Global konaklama tipi tanimlari yalnizca yetkili merkez yoneticileri tarafindan yonetilebilir.", 403);
        }
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecersiz tesis secimi.", 400);
        }

        var tesisExists = await _stysDbContext.Tesisler.AnyAsync(x => x.Id == tesisId && x.AktifMi, cancellationToken);
        if (!tesisExists)
        {
            throw new BaseException("Tesis bulunamadi.", 404);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda islem yapma yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureTesisHasKonaklamaTipiAsync(int tesisId, int konaklamaTipiId, CancellationToken cancellationToken)
    {
        var exists = await _stysDbContext.TesisKonaklamaTipleri.AnyAsync(x =>
            x.TesisId == tesisId
            && x.KonaklamaTipiId == konaklamaTipiId
            && x.AktifMi
            && !x.IsDeleted
            && x.KonaklamaTipi != null
            && x.KonaklamaTipi.AktifMi,
            cancellationToken);

        if (!exists)
        {
            throw new BaseException("Secilen konaklama tipi bu tesiste kullanima acik degil.", 400);
        }
    }

    private async Task EnsureUniqueAsync(KonaklamaTipiDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedKod = dto.Kod.Trim().ToUpperInvariant();
        var normalizedAd = dto.Ad.Trim().ToUpperInvariant();

        var kodExists = await _konaklamaTipiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Kod.ToUpper() == normalizedKod &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (kodExists)
        {
            throw new BaseException("Ayni kod ile aktif bir konaklama tipi zaten mevcut.", 400);
        }

        var adExists = await _konaklamaTipiRepository.AnyAsync(x =>
            x.AktifMi &&
            x.Ad.ToUpper() == normalizedAd &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (adExists)
        {
            throw new BaseException("Ayni isimde aktif bir konaklama tipi zaten mevcut.", 400);
        }
    }

    private static void Normalize(KonaklamaTipiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Konaklama tipi kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Konaklama tipi adi zorunludur.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
        dto.IcerikKalemleri ??= [];
        foreach (var item in dto.IcerikKalemleri)
        {
            item.HizmetKodu = item.HizmetKodu?.Trim() ?? string.Empty;
            item.Periyot = item.Periyot?.Trim() ?? string.Empty;
            item.KullanimTipi = string.IsNullOrWhiteSpace(item.KullanimTipi)
                ? KonaklamaTipiIcerikKullanimTipleri.Adetli
                : item.KullanimTipi.Trim();
            item.KullanimNoktasi = string.IsNullOrWhiteSpace(item.KullanimNoktasi)
                ? KonaklamaTipiIcerikKullanimNoktalari.Genel
                : item.KullanimNoktasi.Trim();
            item.KullanimBaslangicSaati = NormalizeTime(item.KullanimBaslangicSaati);
            item.KullanimBitisSaati = NormalizeTime(item.KullanimBitisSaati);
            item.Aciklama = string.IsNullOrWhiteSpace(item.Aciklama) ? null : item.Aciklama.Trim();
        }
    }

    private static void ValidateIcerikKalemleri(KonaklamaTipiDto dto)
        => ValidateIcerikKalemleri(dto.IcerikKalemleri);

    private static void ValidateIcerikKalemleri(IEnumerable<KonaklamaTipiIcerikDto> items)
    {
        var duplicateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!KonaklamaTipiIcerikHizmetKodlari.IsValid(item.HizmetKodu))
            {
                throw new BaseException("Konaklama tipi iceriginde gecersiz hizmet secildi.", 400);
            }

            if (!KonaklamaTipiIcerikPeriyotlari.IsValid(item.Periyot))
            {
                throw new BaseException("Konaklama tipi iceriginde gecersiz periyot secildi.", 400);
            }

            if (!KonaklamaTipiIcerikKullanimTipleri.IsValid(item.KullanimTipi))
            {
                throw new BaseException("Konaklama tipi iceriginde gecersiz kullanim tipi secildi.", 400);
            }

            if (!KonaklamaTipiIcerikKullanimNoktalari.IsValid(item.KullanimNoktasi))
            {
                throw new BaseException("Konaklama tipi iceriginde gecersiz kullanim noktasi secildi.", 400);
            }

            if (item.Miktar <= 0)
            {
                throw new BaseException("Konaklama tipi icerik miktari sifirdan buyuk olmali.", 400);
            }

            if ((item.KullanimBaslangicSaati is null) != (item.KullanimBitisSaati is null))
            {
                throw new BaseException("Kullanim saat araligi tanimlanacaksa baslangic ve bitis birlikte girilmelidir.", 400);
            }

            if (item.KullanimBaslangicSaati is not null && item.KullanimBitisSaati is not null)
            {
                var baslangic = ParseTime(item.KullanimBaslangicSaati);
                var bitis = ParseTime(item.KullanimBitisSaati);
                if (baslangic >= bitis)
                {
                    throw new BaseException("Kullanim bitis saati baslangic saatinden buyuk olmali.", 400);
                }
            }

            if (!duplicateSet.Add(item.HizmetKodu))
            {
                throw new BaseException("Ayni hizmet bir konaklama tipi icinde birden fazla kez tanimlanamaz.", 400);
            }
        }
    }

    private List<KonaklamaTipiIcerikKalemi> CreateIcerikEntities(KonaklamaTipiDto dto)
        => dto.IcerikKalemleri
            .Select(item => new KonaklamaTipiIcerikKalemi
            {
                HizmetKodu = item.HizmetKodu,
                Miktar = item.Miktar,
                Periyot = item.Periyot,
                KullanimTipi = item.KullanimTipi,
                KullanimNoktasi = item.KullanimNoktasi,
                KullanimBaslangicSaati = ParseTimeOrNull(item.KullanimBaslangicSaati),
                KullanimBitisSaati = ParseTimeOrNull(item.KullanimBitisSaati),
                CheckInGunuGecerliMi = item.CheckInGunuGecerliMi,
                CheckOutGunuGecerliMi = item.CheckOutGunuGecerliMi,
                Aciklama = item.Aciklama
            })
            .ToList();

    private void SyncIcerikKalemleri(KonaklamaTipi entity, KonaklamaTipiDto dto)
    {
        _stysDbContext.KonaklamaTipiIcerikKalemleri.RemoveRange(entity.IcerikKalemleri);
        entity.IcerikKalemleri.Clear();
        foreach (var item in CreateIcerikEntities(dto))
        {
            entity.IcerikKalemleri.Add(item);
        }
    }

    private static Func<IQueryable<KonaklamaTipi>, IQueryable<KonaklamaTipi>> WithIcerik
        => query => query.Include(x => x.IcerikKalemleri.Where(y => !y.IsDeleted));

    private async Task<KonaklamaTipi?> GetEntityWithIcerikAsync(int id)
        => await _konaklamaTipiRepository.GetByIdAsync(id, WithIcerik);

    private static KonaklamaTipiDto MapDto(KonaklamaTipi entity)
    {
        var dto = new KonaklamaTipiDto
        {
            Id = entity.Id,
            Kod = entity.Kod,
            Ad = entity.Ad,
            AktifMi = entity.AktifMi,
            IcerikKalemleri = entity.IcerikKalemleri
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.HizmetKodu)
                .ThenBy(x => x.Id)
                .Select(x => new KonaklamaTipiIcerikDto
                {
                    HizmetKodu = x.HizmetKodu,
                    HizmetAdi = KonaklamaTipiIcerikHizmetKodlari.GetAd(x.HizmetKodu),
                    Miktar = x.Miktar,
                    Periyot = x.Periyot,
                    PeriyotAdi = KonaklamaTipiIcerikPeriyotlari.GetAd(x.Periyot),
                    KullanimTipi = x.KullanimTipi,
                    KullanimTipiAdi = KonaklamaTipiIcerikKullanimTipleri.GetAd(x.KullanimTipi),
                    KullanimNoktasi = x.KullanimNoktasi,
                    KullanimNoktasiAdi = KonaklamaTipiIcerikKullanimNoktalari.GetAd(x.KullanimNoktasi),
                    KullanimBaslangicSaati = FormatTime(x.KullanimBaslangicSaati),
                    KullanimBitisSaati = FormatTime(x.KullanimBitisSaati),
                    CheckInGunuGecerliMi = x.CheckInGunuGecerliMi,
                    CheckOutGunuGecerliMi = x.CheckOutGunuGecerliMi,
                    Aciklama = x.Aciklama
                })
                .ToList()
        };

        return dto;
    }

    private async Task<Dictionary<int, TesisKonaklamaTipiIcerikOverride>> GetTesisOverrideMapAsync(int tesisId, IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return [];
        }

        return await _stysDbContext.Set<TesisKonaklamaTipiIcerikOverride>()
            .Where(x => x.TesisId == tesisId
                && itemIds.Contains(x.KonaklamaTipiIcerikKalemiId)
                && !x.IsDeleted)
            .ToDictionaryAsync(x => x.KonaklamaTipiIcerikKalemiId, cancellationToken);
    }

    private static KonaklamaTipiTesisIcerikOverrideDto BuildTesisOverrideDto(
        KonaklamaTipiIcerikKalemi item,
        TesisKonaklamaTipiIcerikOverride? overrideItem)
    {
        var effective = BuildEffectiveIcerikDto(item, overrideItem);
        return new KonaklamaTipiTesisIcerikOverrideDto
        {
            KonaklamaTipiIcerikKalemiId = item.Id,
            HizmetKodu = item.HizmetKodu,
            HizmetAdi = KonaklamaTipiIcerikHizmetKodlari.GetAd(item.HizmetKodu),
            OverrideVarMi = overrideItem is not null,
            DevreDisiMi = overrideItem?.DevreDisiMi ?? false,
            GlobalMiktar = item.Miktar,
            GlobalPeriyot = item.Periyot,
            GlobalPeriyotAdi = KonaklamaTipiIcerikPeriyotlari.GetAd(item.Periyot),
            GlobalKullanimTipi = item.KullanimTipi,
            GlobalKullanimTipiAdi = KonaklamaTipiIcerikKullanimTipleri.GetAd(item.KullanimTipi),
            GlobalKullanimNoktasi = item.KullanimNoktasi,
            GlobalKullanimNoktasiAdi = KonaklamaTipiIcerikKullanimNoktalari.GetAd(item.KullanimNoktasi),
            GlobalKullanimBaslangicSaati = FormatTime(item.KullanimBaslangicSaati),
            GlobalKullanimBitisSaati = FormatTime(item.KullanimBitisSaati),
            GlobalCheckInGunuGecerliMi = item.CheckInGunuGecerliMi,
            GlobalCheckOutGunuGecerliMi = item.CheckOutGunuGecerliMi,
            GlobalAciklama = item.Aciklama,
            Miktar = effective?.Miktar ?? item.Miktar,
            Periyot = effective?.Periyot ?? item.Periyot,
            PeriyotAdi = effective?.PeriyotAdi ?? KonaklamaTipiIcerikPeriyotlari.GetAd(item.Periyot),
            KullanimTipi = effective?.KullanimTipi ?? item.KullanimTipi,
            KullanimTipiAdi = effective?.KullanimTipiAdi ?? KonaklamaTipiIcerikKullanimTipleri.GetAd(item.KullanimTipi),
            KullanimNoktasi = effective?.KullanimNoktasi ?? item.KullanimNoktasi,
            KullanimNoktasiAdi = effective?.KullanimNoktasiAdi ?? KonaklamaTipiIcerikKullanimNoktalari.GetAd(item.KullanimNoktasi),
            KullanimBaslangicSaati = effective?.KullanimBaslangicSaati ?? FormatTime(item.KullanimBaslangicSaati),
            KullanimBitisSaati = effective?.KullanimBitisSaati ?? FormatTime(item.KullanimBitisSaati),
            CheckInGunuGecerliMi = effective?.CheckInGunuGecerliMi ?? item.CheckInGunuGecerliMi,
            CheckOutGunuGecerliMi = effective?.CheckOutGunuGecerliMi ?? item.CheckOutGunuGecerliMi,
            Aciklama = effective?.Aciklama ?? item.Aciklama
        };
    }

    private static KonaklamaTipiIcerikDto? BuildEffectiveIcerikDto(
        KonaklamaTipiIcerikKalemi item,
        TesisKonaklamaTipiIcerikOverride? overrideItem)
    {
        if (overrideItem?.DevreDisiMi == true)
        {
            return null;
        }

        var miktar = overrideItem?.Miktar ?? item.Miktar;
        var periyot = overrideItem?.Periyot ?? item.Periyot;
        var kullanimTipi = overrideItem?.KullanimTipi ?? item.KullanimTipi;
        var kullanimNoktasi = overrideItem?.KullanimNoktasi ?? item.KullanimNoktasi;
        var baslangic = overrideItem?.KullanimBaslangicSaati ?? item.KullanimBaslangicSaati;
        var bitis = overrideItem?.KullanimBitisSaati ?? item.KullanimBitisSaati;
        var checkIn = overrideItem?.CheckInGunuGecerliMi ?? item.CheckInGunuGecerliMi;
        var checkOut = overrideItem?.CheckOutGunuGecerliMi ?? item.CheckOutGunuGecerliMi;
        var aciklama = overrideItem?.Aciklama ?? item.Aciklama;

        return new KonaklamaTipiIcerikDto
        {
            HizmetKodu = item.HizmetKodu,
            HizmetAdi = KonaklamaTipiIcerikHizmetKodlari.GetAd(item.HizmetKodu),
            Miktar = miktar,
            Periyot = periyot,
            PeriyotAdi = KonaklamaTipiIcerikPeriyotlari.GetAd(periyot),
            KullanimTipi = kullanimTipi,
            KullanimTipiAdi = KonaklamaTipiIcerikKullanimTipleri.GetAd(kullanimTipi),
            KullanimNoktasi = kullanimNoktasi,
            KullanimNoktasiAdi = KonaklamaTipiIcerikKullanimNoktalari.GetAd(kullanimNoktasi),
            KullanimBaslangicSaati = FormatTime(baslangic),
            KullanimBitisSaati = FormatTime(bitis),
            CheckInGunuGecerliMi = checkIn,
            CheckOutGunuGecerliMi = checkOut,
            Aciklama = aciklama
        };
    }

    private static void ValidateOverrideItem(KonaklamaTipiTesisIcerikOverrideDto item)
    {
        if (item.DevreDisiMi)
        {
            return;
        }

        ValidateIcerikKalemleri([
            new KonaklamaTipiIcerikDto
            {
                HizmetKodu = item.HizmetKodu,
                Miktar = item.Miktar,
                Periyot = item.Periyot,
                KullanimTipi = item.KullanimTipi,
                KullanimNoktasi = item.KullanimNoktasi,
                KullanimBaslangicSaati = item.KullanimBaslangicSaati,
                KullanimBitisSaati = item.KullanimBitisSaati,
                CheckInGunuGecerliMi = item.CheckInGunuGecerliMi,
                CheckOutGunuGecerliMi = item.CheckOutGunuGecerliMi,
                Aciklama = item.Aciklama
            }
        ]);
    }

    private static TesisKonaklamaTipiIcerikOverride BuildNormalizedOverride(
        int tesisId,
        KonaklamaTipiIcerikKalemi globalItem,
        KonaklamaTipiTesisIcerikOverrideDto item)
    {
        var normalizedPeriyot = item.DevreDisiMi ? null : item.Periyot?.Trim();
        var normalizedKullanimTipi = item.DevreDisiMi ? null : item.KullanimTipi?.Trim();
        var normalizedKullanimNoktasi = item.DevreDisiMi ? null : item.KullanimNoktasi?.Trim();
        var normalizedAciklama = string.IsNullOrWhiteSpace(item.Aciklama) ? null : item.Aciklama.Trim();
        var normalizedBaslangic = ParseTimeOrNull(NormalizeTime(item.KullanimBaslangicSaati));
        var normalizedBitis = ParseTimeOrNull(NormalizeTime(item.KullanimBitisSaati));

        return new TesisKonaklamaTipiIcerikOverride
        {
            TesisId = tesisId,
            KonaklamaTipiIcerikKalemiId = globalItem.Id,
            DevreDisiMi = item.DevreDisiMi,
            Miktar = item.DevreDisiMi || item.Miktar == globalItem.Miktar ? null : item.Miktar,
            Periyot = item.DevreDisiMi || string.Equals(normalizedPeriyot, globalItem.Periyot, StringComparison.OrdinalIgnoreCase) ? null : normalizedPeriyot,
            KullanimTipi = item.DevreDisiMi || string.Equals(normalizedKullanimTipi, globalItem.KullanimTipi, StringComparison.OrdinalIgnoreCase) ? null : normalizedKullanimTipi,
            KullanimNoktasi = item.DevreDisiMi || string.Equals(normalizedKullanimNoktasi, globalItem.KullanimNoktasi, StringComparison.OrdinalIgnoreCase) ? null : normalizedKullanimNoktasi,
            KullanimBaslangicSaati = item.DevreDisiMi || normalizedBaslangic == globalItem.KullanimBaslangicSaati ? null : normalizedBaslangic,
            KullanimBitisSaati = item.DevreDisiMi || normalizedBitis == globalItem.KullanimBitisSaati ? null : normalizedBitis,
            CheckInGunuGecerliMi = item.DevreDisiMi || item.CheckInGunuGecerliMi == globalItem.CheckInGunuGecerliMi ? null : item.CheckInGunuGecerliMi,
            CheckOutGunuGecerliMi = item.DevreDisiMi || item.CheckOutGunuGecerliMi == globalItem.CheckOutGunuGecerliMi ? null : item.CheckOutGunuGecerliMi,
            Aciklama = item.DevreDisiMi || string.Equals(normalizedAciklama, globalItem.Aciklama, StringComparison.Ordinal) ? null : normalizedAciklama,
        };
    }

    private static bool HasMeaningfulOverride(TesisKonaklamaTipiIcerikOverride item)
        => item.DevreDisiMi
            || item.Miktar.HasValue
            || item.Periyot is not null
            || item.KullanimTipi is not null
            || item.KullanimNoktasi is not null
            || item.KullanimBaslangicSaati.HasValue
            || item.KullanimBitisSaati.HasValue
            || item.CheckInGunuGecerliMi.HasValue
            || item.CheckOutGunuGecerliMi.HasValue
            || item.Aciklama is not null;

    private async Task<HashSet<int>> GetKilitliKonaklamaTipiIdsAsync(int tesisId, IReadOnlyCollection<int> konaklamaTipiIds, CancellationToken cancellationToken)
    {
        var fiyatKayitlari = await (
                from fiyat in _stysDbContext.OdaFiyatlari
                join odaTipi in _stysDbContext.OdaTipleri on fiyat.TesisOdaTipiId equals odaTipi.Id
                where konaklamaTipiIds.Contains(fiyat.KonaklamaTipiId)
                      && odaTipi.TesisId == tesisId
                select fiyat.KonaklamaTipiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rezervasyonKayitlari = await _stysDbContext.Rezervasyonlar
            .Where(x => x.TesisId == tesisId && x.KonaklamaTipiId.HasValue && konaklamaTipiIds.Contains(x.KonaklamaTipiId.Value))
            .Select(x => x.KonaklamaTipiId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var tesisIndirimKayitlari = await _stysDbContext.IndirimKuraliKonaklamaTipleri
            .Where(x => konaklamaTipiIds.Contains(x.KonaklamaTipiId)
                && x.IndirimKurali != null
                && x.IndirimKurali.TesisId == tesisId)
            .Select(x => x.KonaklamaTipiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fiyatKayitlari
            .Concat(rezervasyonKayitlari)
            .Concat(tesisIndirimKayitlari)
            .ToHashSet();
    }

    private static string? NormalizeTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parsed = ParseTime(value);
        return FormatTime(parsed);
    }

    private static TimeSpan ParseTime(string value)
    {
        if (!TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var parsed))
        {
            throw new BaseException("Kullanim saati HH:mm formatinda olmalidir.", 400);
        }

        return parsed;
    }

    private static TimeSpan? ParseTimeOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ParseTime(value);

    private static string? FormatTime(TimeSpan? value)
        => value.HasValue ? value.Value.ToString(@"hh\:mm", CultureInfo.InvariantCulture) : null;
}
