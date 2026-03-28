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

    public async Task<List<EkHizmetDto>> GetHizmetlerByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<EkHizmetDto>>(items);
    }

    public async Task<List<EkHizmetDto>> UpsertHizmetlerByTesisAsync(int tesisId, IEnumerable<EkHizmetDto> hizmetler, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);
        var items = (hizmetler ?? []).ToList();

        foreach (var item in items)
        {
            item.TesisId = tesisId;
            Normalize(item);
        }

        ValidateHizmetRows(items);

        var existing = await _stysDbContext.EkHizmetler
            .Where(x => x.TesisId == tesisId)
            .ToListAsync(cancellationToken);

        var existingById = existing.ToDictionary(x => x.Id);
        foreach (var item in items)
        {
            if (item.Id is > 0 && existingById.TryGetValue(item.Id.Value, out var entity))
            {
                entity.Ad = item.Ad;
                entity.Aciklama = item.Aciklama;
                entity.BirimAdi = item.BirimAdi;
                entity.PaketIcerikHizmetKodu = item.PaketIcerikHizmetKodu;
                entity.AktifMi = item.AktifMi;
                continue;
            }

            await _stysDbContext.EkHizmetler.AddAsync(new EkHizmet
            {
                TesisId = tesisId,
                Ad = item.Ad,
                Aciklama = item.Aciklama,
                BirimAdi = item.BirimAdi,
                PaketIcerikHizmetKodu = item.PaketIcerikHizmetKodu,
                AktifMi = item.AktifMi
            }, cancellationToken);
        }

        var incomingIds = items.Where(x => x.Id > 0).Select(x => x.Id).ToHashSet();
        var toRemove = existing.Where(x => !incomingIds.Contains(x.Id)).ToList();
        if (toRemove.Count > 0)
        {
            var ids = toRemove.Select(x => x.Id).ToList();
            var referencedIds = await GetReferencedEkHizmetIdsAsync(ids, cancellationToken);

            foreach (var entity in toRemove)
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
        return await GetHizmetlerByTesisIdAsync(tesisId, cancellationToken);
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

    private static void Normalize(EkHizmetDto item)
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

    private static void ValidateHizmetRows(IReadOnlyCollection<EkHizmetDto> items)
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
