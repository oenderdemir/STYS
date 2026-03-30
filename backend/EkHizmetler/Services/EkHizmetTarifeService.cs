using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.EkHizmetler.Dto;
using STYS.EkHizmetler.Entities;
using STYS.KonaklamaTipleri;
using STYS.EkHizmetler.Repositories;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.EkHizmetler.Services;

public class EkHizmetTarifeService : BaseRdbmsService<EkHizmetTarifeDto, EkHizmetTarife, int>, IEkHizmetTarifeService
{
    private readonly IEkHizmetTarifeRepository _ekHizmetTarifeRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public EkHizmetTarifeService(
        IEkHizmetTarifeRepository ekHizmetTarifeRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(ekHizmetTarifeRepository, mapper)
    {
        _ekHizmetTarifeRepository = ekHizmetTarifeRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public async Task<List<EkHizmetTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default)
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
            .Select(x => new EkHizmetTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<GlobalEkHizmetTanimiDto>> GetGlobalTanimlarAsync(CancellationToken cancellationToken = default)
    {
        var items = await _stysDbContext.GlobalEkHizmetTanimlari
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<GlobalEkHizmetTanimiDto>>(items);
    }

    public async Task<GlobalEkHizmetTanimiDto?> GetGlobalTanimByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _stysDbContext.GlobalEkHizmetTanimlari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : Mapper.Map<GlobalEkHizmetTanimiDto>(entity);
    }

    public async Task<GlobalEkHizmetTanimiDto> AddGlobalTanimAsync(GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageGlobalAsync(cancellationToken);
        Normalize(dto);
        ValidateGlobalRows([dto]);
        await EnsureUniqueGlobalAsync(dto, null, cancellationToken);

        var entity = Mapper.Map<GlobalEkHizmetTanimi>(dto);
        await _stysDbContext.GlobalEkHizmetTanimlari.AddAsync(entity, cancellationToken);
        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return Mapper.Map<GlobalEkHizmetTanimiDto>(entity);
    }

    public async Task<GlobalEkHizmetTanimiDto> UpdateGlobalTanimAsync(int id, GlobalEkHizmetTanimiDto dto, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageGlobalAsync(cancellationToken);
        Normalize(dto);
        ValidateGlobalRows([dto]);
        await EnsureUniqueGlobalAsync(dto, id, cancellationToken);

        var entity = await _stysDbContext.GlobalEkHizmetTanimlari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Guncellenecek global ek hizmet tanimi bulunamadi.", 404);

        entity.Ad = dto.Ad;
        entity.Aciklama = dto.Aciklama;
        entity.BirimAdi = dto.BirimAdi;
        entity.PaketIcerikHizmetKodu = dto.PaketIcerikHizmetKodu;
        entity.AktifMi = dto.AktifMi;

        var assignments = await _stysDbContext.EkHizmetler
            .Where(x => x.GlobalEkHizmetTanimiId == id)
            .ToListAsync(cancellationToken);

        foreach (var assignment in assignments)
        {
            assignment.Ad = entity.Ad;
            assignment.Aciklama = entity.Aciklama;
            assignment.BirimAdi = entity.BirimAdi;
            assignment.PaketIcerikHizmetKodu = entity.PaketIcerikHizmetKodu;
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return Mapper.Map<GlobalEkHizmetTanimiDto>(entity);
    }

    public async Task DeleteGlobalTanimAsync(int id, CancellationToken cancellationToken = default)
    {
        await EnsureCanManageGlobalAsync(cancellationToken);

        var entity = await _stysDbContext.GlobalEkHizmetTanimlari.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Silinecek global ek hizmet tanimi bulunamadi.", 404);

        var hasAssignments = await _stysDbContext.EkHizmetler.AnyAsync(x => x.GlobalEkHizmetTanimiId == id, cancellationToken);
        if (hasAssignments)
        {
            entity.AktifMi = false;
            await _stysDbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        _stysDbContext.GlobalEkHizmetTanimlari.Remove(entity);
        await _stysDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<EkHizmetTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var assignments = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId && x.GlobalEkHizmetTanimiId.HasValue)
            .Select(x => new
            {
                x.GlobalEkHizmetTanimiId,
                x.AktifMi,
                TarifeSayisi = x.Tarifeler.Count(t => !t.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        var assignmentMap = assignments
            .Where(x => x.GlobalEkHizmetTanimiId.HasValue)
            .ToDictionary(x => x.GlobalEkHizmetTanimiId!.Value, x => new { x.AktifMi, x.TarifeSayisi });

        var globals = await _stysDbContext.GlobalEkHizmetTanimlari
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return globals.Select(x => new EkHizmetTesisAtamaDto
        {
            GlobalEkHizmetTanimiId = x.Id,
            Ad = x.Ad,
            Aciklama = x.Aciklama,
            BirimAdi = x.BirimAdi,
            PaketIcerikHizmetKodu = x.PaketIcerikHizmetKodu,
            GlobalAktifMi = x.AktifMi,
            TesisteKullanilabilirMi = assignmentMap.ContainsKey(x.Id) && assignmentMap[x.Id].AktifMi,
            TarifeSayisi = assignmentMap.ContainsKey(x.Id) ? assignmentMap[x.Id].TarifeSayisi : 0
        }).ToList();
    }

    public async Task<List<EkHizmetTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> globalTanimIds, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var hedefIds = (globalTanimIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var globals = await _stysDbContext.GlobalEkHizmetTanimlari
            .Where(x => hedefIds.Contains(x.Id) && x.AktifMi)
            .ToListAsync(cancellationToken);

        if (globals.Count != hedefIds.Count)
        {
            throw new BaseException("Secilen global ek hizmet tanimlarindan biri gecersiz veya pasif.", 400);
        }

        var mevcutlar = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId && x.GlobalEkHizmetTanimiId.HasValue)
            .Include(x => x.Tarifeler)
            .ToListAsync(cancellationToken);

        var mevcutByGlobalId = mevcutlar.ToDictionary(x => x.GlobalEkHizmetTanimiId!.Value);
        foreach (var global in globals)
        {
            if (mevcutByGlobalId.TryGetValue(global.Id, out var mevcut))
            {
                mevcut.Ad = global.Ad;
                mevcut.Aciklama = global.Aciklama;
                mevcut.BirimAdi = global.BirimAdi;
                mevcut.PaketIcerikHizmetKodu = global.PaketIcerikHizmetKodu;
                mevcut.AktifMi = true;
                mevcut.IsDeleted = false;
                continue;
            }

            await _stysDbContext.EkHizmetler.AddAsync(new EkHizmet
            {
                TesisId = tesisId,
                GlobalEkHizmetTanimiId = global.Id,
                Ad = global.Ad,
                Aciklama = global.Aciklama,
                BirimAdi = global.BirimAdi,
                PaketIcerikHizmetKodu = global.PaketIcerikHizmetKodu,
                AktifMi = true
            }, cancellationToken);
        }

        var deselected = mevcutlar.Where(x => !hedefIds.Contains(x.GlobalEkHizmetTanimiId!.Value)).ToList();
        if (deselected.Count > 0)
        {
            var referencedIds = await GetReferencedEkHizmetIdsAsync(deselected.Select(x => x.Id).ToList(), cancellationToken);
            foreach (var entity in deselected)
            {
                if (referencedIds.Contains(entity.Id))
                {
                    entity.AktifMi = false;
                    continue;
                }

                _stysDbContext.EkHizmetler.Remove(entity);
            }
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return await GetTesisAtamalariAsync(tesisId, cancellationToken);
    }

    public async Task<List<EkHizmetDto>> GetHizmetlerByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId && x.AktifMi)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<EkHizmetDto>>(items);
    }

    public async Task<List<EkHizmetTarifeDto>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _ekHizmetTarifeRepository.Where(x => x.TesisId == tesisId)
            .Include(x => x.EkHizmet)
            .OrderBy(x => x.EkHizmet!.Ad)
            .ThenBy(x => x.BaslangicTarihi)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<EkHizmetTarifeDto>>(items);
    }

    public async Task<List<EkHizmetTarifeDto>> UpsertByTesisAsync(int tesisId, IEnumerable<EkHizmetTarifeDto> tarifeler, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        var items = (tarifeler ?? []).ToList();

        foreach (var item in items)
        {
            item.TesisId = tesisId;
            Normalize(item);
        }

        var tesisHizmetleri = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        ValidateTarifeRows(items, tesisHizmetleri);

        var existing = await _ekHizmetTarifeRepository.Where(x => x.TesisId == tesisId).ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(x => x.Id);

        foreach (var item in items)
        {
            if (item.Id is > 0 && existingById.TryGetValue(item.Id.Value, out var entity))
            {
                entity.EkHizmetId = item.EkHizmetId;
                entity.BirimFiyat = item.BirimFiyat;
                entity.ParaBirimi = item.ParaBirimi;
                entity.BaslangicTarihi = item.BaslangicTarihi;
                entity.BitisTarihi = item.BitisTarihi;
                entity.AktifMi = item.AktifMi;
                continue;
            }

            await _ekHizmetTarifeRepository.AddAsync(new EkHizmetTarife
            {
                TesisId = tesisId,
                EkHizmetId = item.EkHizmetId,
                BirimFiyat = item.BirimFiyat,
                ParaBirimi = item.ParaBirimi,
                BaslangicTarihi = item.BaslangicTarihi,
                BitisTarihi = item.BitisTarihi,
                AktifMi = item.AktifMi
            });
        }

        var incomingIds = items.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
        var toRemove = existing.Where(x => !incomingIds.Contains(x.Id)).ToList();
        if (toRemove.Count > 0)
        {
            var ids = toRemove.Select(x => x.Id).ToList();
            var referencedIds = await _stysDbContext.RezervasyonEkHizmetler
                .Where(x => ids.Contains(x.EkHizmetTarifeId))
                .Select(x => x.EkHizmetTarifeId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var referencedIdSet = referencedIds.ToHashSet();
            foreach (var entity in toRemove)
            {
                if (referencedIdSet.Contains(entity.Id))
                {
                    entity.AktifMi = false;
                    continue;
                }

                _ekHizmetTarifeRepository.Delete(entity);
            }
        }

        await _ekHizmetTarifeRepository.SaveChangesAsync(cancellationToken);
        return await GetByTesisIdAsync(tesisId, cancellationToken);
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

    private async Task<HashSet<int>> GetReferencedEkHizmetIdsAsync(IReadOnlyCollection<int> ekHizmetIds, CancellationToken cancellationToken)
    {
        var tarifedenGelenler = await _stysDbContext.EkHizmetTarifeleri
            .Where(x => ekHizmetIds.Contains(x.EkHizmetId))
            .Select(x => x.EkHizmetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rezervasyondanGelenler = await _stysDbContext.RezervasyonEkHizmetler
            .Where(x => ekHizmetIds.Contains(x.EkHizmetId))
            .Select(x => x.EkHizmetId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return tarifedenGelenler.Concat(rezervasyondanGelenler).ToHashSet();
    }

    private async Task EnsureCanManageGlobalAsync(CancellationToken cancellationToken)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        if (scope.IsScoped)
        {
            throw new BaseException("Global ek hizmet tanimlari yalnizca merkez yonetimi tarafindan duzenlenebilir.", 403);
        }
    }

    private static void Normalize(GlobalEkHizmetTanimiDto item)
    {
        item.Ad = item.Ad?.Trim() ?? string.Empty;
        item.Aciklama = string.IsNullOrWhiteSpace(item.Aciklama) ? null : item.Aciklama.Trim();
        item.BirimAdi = string.IsNullOrWhiteSpace(item.BirimAdi) ? "Adet" : item.BirimAdi.Trim();
        item.PaketIcerikHizmetKodu = string.IsNullOrWhiteSpace(item.PaketIcerikHizmetKodu)
            ? null
            : item.PaketIcerikHizmetKodu.Trim();
    }

    private static void Normalize(EkHizmetTarifeDto item)
    {
        item.ParaBirimi = string.IsNullOrWhiteSpace(item.ParaBirimi) ? "TRY" : item.ParaBirimi.Trim().ToUpperInvariant();
        item.BaslangicTarihi = item.BaslangicTarihi.Date;
        item.BitisTarihi = item.BitisTarihi.Date;
    }

    private static void ValidateGlobalRows(IReadOnlyCollection<GlobalEkHizmetTanimiDto> items)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Ad))
            {
                throw new BaseException("Ek hizmet adi zorunludur.", 400);
            }

            if (string.IsNullOrWhiteSpace(item.BirimAdi))
            {
                throw new BaseException("Birim adi zorunludur.", 400);
            }

            if (item.PaketIcerikHizmetKodu is not null && !KonaklamaTipiIcerikHizmetKodlari.IsValid(item.PaketIcerikHizmetKodu))
            {
                throw new BaseException($"'{item.Ad}' hizmeti icin gecersiz paket icerik eslesmesi secildi.", 400);
            }
        }

        var duplicates = items
            .GroupBy(x => x.Ad.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicates is not null)
        {
            throw new BaseException($"'{duplicates.Key}' hizmeti birden fazla kez tanimlanamaz.", 400);
        }
    }

    private async Task EnsureUniqueGlobalAsync(GlobalEkHizmetTanimiDto item, int? excludedId, CancellationToken cancellationToken)
    {
        var normalizedName = item.Ad.Trim();
        var exists = await _stysDbContext.GlobalEkHizmetTanimlari.AnyAsync(
            x => x.Ad == normalizedName
                && (!excludedId.HasValue || x.Id != excludedId.Value),
            cancellationToken);

        if (exists)
        {
            throw new BaseException($"'{item.Ad}' adinda baska bir global ek hizmet tanimi zaten bulunuyor.", 400);
        }
    }

    private static void ValidateTarifeRows(IReadOnlyCollection<EkHizmetTarifeDto> items, IReadOnlyDictionary<int, EkHizmet> tesisHizmetleri)
    {
        foreach (var item in items)
        {
            if (item.EkHizmetId <= 0 || !tesisHizmetleri.ContainsKey(item.EkHizmetId))
            {
                throw new BaseException("Gecerli bir ek hizmet seciniz.", 400);
            }

            if (item.BirimFiyat < 0)
            {
                throw new BaseException("Birim fiyat negatif olamaz.", 400);
            }

            if (item.BaslangicTarihi > item.BitisTarihi)
            {
                throw new BaseException("Baslangic tarihi bitis tarihinden buyuk olamaz.", 400);
            }

            if (item.ParaBirimi.Length is < 3 or > 3)
            {
                throw new BaseException("Para birimi 3 karakter olmalidir.", 400);
            }
        }

        var groups = items
            .GroupBy(x => x.EkHizmetId)
            .ToList();

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(x => x.BaslangicTarihi)
                .ThenBy(x => x.BitisTarihi)
                .ToList();

            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].BaslangicTarihi <= ordered[i - 1].BitisTarihi)
                {
                    var hizmetAdi = tesisHizmetleri[group.Key].Ad;
                    throw new BaseException($"'{hizmetAdi}' hizmeti icin cakisan tarih araligi tanimlanamaz.", 400);
                }
            }
        }
    }
}
