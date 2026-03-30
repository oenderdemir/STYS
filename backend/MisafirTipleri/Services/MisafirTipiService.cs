using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.MisafirTipleri.Dto;
using STYS.MisafirTipleri.Entities;
using STYS.MisafirTipleri.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.MisafirTipleri.Services;

public class MisafirTipiService : BaseRdbmsService<MisafirTipiDto, MisafirTipi, int>, IMisafirTipiService
{
    private readonly IMisafirTipiRepository _misafirTipiRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public MisafirTipiService(
        IMisafirTipiRepository misafirTipiRepository,
        IUserAccessScopeService userAccessScopeService,
        StysAppDbContext stysDbContext,
        IMapper mapper)
        : base(misafirTipiRepository, mapper)
    {
        _misafirTipiRepository = misafirTipiRepository;
        _userAccessScopeService = userAccessScopeService;
        _stysDbContext = stysDbContext;
    }

    public override async Task<MisafirTipiDto> AddAsync(MisafirTipiDto dto)
    {
        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, null);

        var entity = Mapper.Map<MisafirTipi>(dto);
        await _misafirTipiRepository.AddAsync(entity);
        await _misafirTipiRepository.SaveChangesAsync();
        return Mapper.Map<MisafirTipiDto>(entity);
    }

    public override async Task<MisafirTipiDto> UpdateAsync(MisafirTipiDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Misafir tipi id zorunludur.", 400);
        }

        await EnsureCanManageGlobalAsync();
        Normalize(dto);
        await EnsureUniqueAsync(dto, dto.Id.Value);

        var entity = await _misafirTipiRepository.GetByIdAsync(dto.Id.Value);
        if (entity is null)
        {
            throw new BaseException("Guncellenecek misafir tipi bulunamadi.", 404);
        }

        entity.IsDeleted = false;
        entity.Kod = dto.Kod;
        entity.Ad = dto.Ad;
        entity.AktifMi = dto.AktifMi;

        _misafirTipiRepository.Update(entity);
        await _misafirTipiRepository.SaveChangesAsync();
        return Mapper.Map<MisafirTipiDto>(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanManageGlobalAsync();

        var entity = await _misafirTipiRepository.GetByIdAsync(id);
        if (entity is null)
        {
            return;
        }

        var kilitliIdler = await GetKilitliMisafirTipiIdsAsync([id]);
        if (kilitliIdler.Contains(id))
        {
            throw new BaseException($"\"{entity.Ad}\" misafir tipi fiyat, indirim veya rezervasyon kaydinda kullanildigi icin silinemez.", 400);
        }

        await base.DeleteAsync(id);
    }

    public async Task<MisafirTipiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync(cancellationToken);
        var query = _stysDbContext.Tesisler.Where(x => x.AktifMi);

        if (scope.IsScoped)
        {
            query = query.Where(x => scope.TesisIds.Contains(x.Id));
        }

        var tesisler = await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .Select(x => new MisafirTipiTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new MisafirTipiYonetimBaglamDto
        {
            GlobalTipYonetimiYapabilirMi = !scope.IsScoped,
            Tesisler = tesisler
        };
    }

    public async Task<List<MisafirTipiTesisAtamaDto>> GetTesisAtamalariAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var seciliTipIds = await _stysDbContext.TesisMisafirTipleri
            .Where(x => x.TesisId == tesisId && x.AktifMi && !x.IsDeleted)
            .Select(x => x.MisafirTipiId)
            .ToListAsync(cancellationToken);
        var seciliSet = seciliTipIds.ToHashSet();

        var tipler = await _stysDbContext.MisafirTipleri
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return tipler.Select(x => new MisafirTipiTesisAtamaDto
        {
            MisafirTipiId = x.Id,
            Kod = x.Kod,
            Ad = x.Ad,
            GlobalAktifMi = x.AktifMi,
            TesisteKullanilabilirMi = seciliSet.Contains(x.Id)
        }).ToList();
    }

    public async Task<List<MisafirTipiTesisAtamaDto>> KaydetTesisAtamalariAsync(int tesisId, IReadOnlyCollection<int> misafirTipiIds, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var hedefIdler = (misafirTipiIds ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var hedefTipler = await _stysDbContext.MisafirTipleri
            .Where(x => hedefIdler.Contains(x.Id) && x.AktifMi)
            .Select(x => new { x.Id, x.Ad })
            .ToListAsync(cancellationToken);

        if (hedefTipler.Count != hedefIdler.Count)
        {
            throw new BaseException("Secilen misafir tiplerinden biri gecersiz veya pasif.", 400);
        }

        var mevcutlar = await _stysDbContext.TesisMisafirTipleri
            .Where(x => x.TesisId == tesisId)
            .ToListAsync(cancellationToken);

        var kaldirilacakIdler = mevcutlar
            .Where(x => x.AktifMi && !x.IsDeleted && !hedefIdler.Contains(x.MisafirTipiId))
            .Select(x => x.MisafirTipiId)
            .Distinct()
            .ToList();

        if (kaldirilacakIdler.Count > 0)
        {
            var kilitliIdler = await GetKilitliMisafirTipiIdsAsync(kaldirilacakIdler, tesisId, cancellationToken);
            if (kilitliIdler.Count > 0)
            {
                var kilitliAdlar = await _stysDbContext.MisafirTipleri
                    .Where(x => kilitliIdler.Contains(x.Id))
                    .OrderBy(x => x.Ad)
                    .Select(x => x.Ad)
                    .ToListAsync(cancellationToken);

                throw new BaseException(
                    $"Su misafir tipleri ilgili tesiste fiyat, indirim veya rezervasyon kaydinda kullanildigi icin kaldirilamaz: {string.Join(", ", kilitliAdlar)}.",
                    400);
            }
        }

        var mevcutByTipId = mevcutlar.ToDictionary(x => x.MisafirTipiId);
        foreach (var tipId in hedefIdler)
        {
            if (mevcutByTipId.TryGetValue(tipId, out var mevcut))
            {
                mevcut.IsDeleted = false;
                mevcut.AktifMi = true;
                continue;
            }

            await _stysDbContext.TesisMisafirTipleri.AddAsync(new TesisMisafirTipi
            {
                TesisId = tesisId,
                MisafirTipiId = tipId,
                AktifMi = true
            }, cancellationToken);
        }

        foreach (var mevcut in mevcutlar.Where(x => !hedefIdler.Contains(x.MisafirTipiId)))
        {
            mevcut.AktifMi = false;
        }

        await _stysDbContext.SaveChangesAsync(cancellationToken);
        return await GetTesisAtamalariAsync(tesisId, cancellationToken);
    }

    public async Task<List<MisafirTipiDto>> GetAktifMisafirTipleriByTesisAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var items = await _stysDbContext.MisafirTipleri
            .Where(x => x.AktifMi
                && _stysDbContext.TesisMisafirTipleri.Any(y =>
                    y.TesisId == tesisId
                    && y.MisafirTipiId == x.Id
                    && y.AktifMi
                    && !y.IsDeleted))
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        return Mapper.Map<List<MisafirTipiDto>>(items);
    }

    private async Task EnsureCanManageGlobalAsync()
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped)
        {
            throw new BaseException("Global misafir tipi tanimlari yalnizca yetkili merkez yoneticileri tarafindan yonetilebilir.", 403);
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

    private async Task EnsureUniqueAsync(MisafirTipiDto dto, int? excludeId)
    {
        var kod = dto.Kod;
        var ad = dto.Ad;

        var duplicateExists = await _stysDbContext.MisafirTipleri.AnyAsync(x =>
            (excludeId == null || x.Id != excludeId.Value)
            && (x.Kod == kod || x.Ad == ad));

        if (duplicateExists)
        {
            throw new BaseException("Ayni kod veya ada sahip baska bir misafir tipi zaten mevcut.", 400);
        }
    }

    private static void Normalize(MisafirTipiDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Kod))
        {
            throw new BaseException("Misafir tipi kodu zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Misafir tipi adi zorunludur.", 400);
        }

        dto.Kod = dto.Kod.Trim().ToUpperInvariant();
        dto.Ad = dto.Ad.Trim();
    }

    private async Task<HashSet<int>> GetKilitliMisafirTipiIdsAsync(IReadOnlyCollection<int> misafirTipiIds, int? tesisId = null, CancellationToken cancellationToken = default)
    {
        var fiyatKayitlariQuery = _stysDbContext.OdaFiyatlari
            .Where(x => misafirTipiIds.Contains(x.MisafirTipiId));

        if (tesisId.HasValue)
        {
            fiyatKayitlariQuery =
                from fiyat in fiyatKayitlariQuery
                join odaTipi in _stysDbContext.OdaTipleri on fiyat.TesisOdaTipiId equals odaTipi.Id
                where odaTipi.TesisId == tesisId.Value
                select fiyat;
        }

        var fiyatKayitlari = await fiyatKayitlariQuery
            .Select(x => x.MisafirTipiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var rezervasyonKayitlariQuery = _stysDbContext.Rezervasyonlar
            .Where(x => x.MisafirTipiId.HasValue && misafirTipiIds.Contains(x.MisafirTipiId.Value));

        if (tesisId.HasValue)
        {
            rezervasyonKayitlariQuery = rezervasyonKayitlariQuery.Where(x => x.TesisId == tesisId.Value);
        }

        var rezervasyonKayitlari = await rezervasyonKayitlariQuery
            .Select(x => x.MisafirTipiId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var indirimKayitlariQuery = _stysDbContext.IndirimKuraliMisafirTipleri
            .Where(x => misafirTipiIds.Contains(x.MisafirTipiId));

        if (tesisId.HasValue)
        {
            indirimKayitlariQuery = indirimKayitlariQuery
                .Where(x => x.IndirimKurali != null && x.IndirimKurali.TesisId == tesisId.Value);
        }

        var indirimKayitlari = await indirimKayitlariQuery
            .Select(x => x.MisafirTipiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return fiyatKayitlari
            .Concat(rezervasyonKayitlari)
            .Concat(indirimKayitlari)
            .ToHashSet();
    }
}
