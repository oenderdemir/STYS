using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Repositories;
using STYS.Muhasebe.Hesaplar.Dtos;
using STYS.Muhasebe.Hesaplar.Entities;
using STYS.Muhasebe.Hesaplar.Repositories;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.KasaBankaHesaplari.Repositories;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.Hesaplar.Services;

public class HesapService : BaseRdbmsService<HesapDto, Hesap, int>, IHesapService
{
    private readonly IHesapRepository _hesapRepository;
    private readonly IMuhasebeHesapPlaniRepository _muhasebeHesapPlaniRepository;
    private readonly IKasaBankaHesapRepository _kasaBankaHesapRepository;
    private readonly IDepoRepository _depoRepository;
    private readonly StysAppDbContext _dbContext;

    public HesapService(
        IHesapRepository hesapRepository,
        IMuhasebeHesapPlaniRepository muhasebeHesapPlaniRepository,
        IKasaBankaHesapRepository kasaBankaHesapRepository,
        IDepoRepository depoRepository,
        StysAppDbContext dbContext,
        IMapper mapper) : base(hesapRepository, mapper)
    {
        _hesapRepository = hesapRepository;
        _muhasebeHesapPlaniRepository = muhasebeHesapPlaniRepository;
        _kasaBankaHesapRepository = kasaBankaHesapRepository;
        _depoRepository = depoRepository;
        _dbContext = dbContext;
    }

    public async Task<List<HesapDto>> GetDetailedListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _hesapRepository.GetAllWithDetailsAsync(cancellationToken);
        return items.Select(MapDetailDto).ToList();
    }

    public async Task<HesapDto?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _hesapRepository.GetDetailByIdAsync(id, cancellationToken);
        return item is null ? null : MapDetailDto(item);
    }

    public async Task<List<HesapLookupDto>> GetKasaHesapLookupsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _kasaBankaHesapRepository.GetByTipAsync(KasaBankaHesapTipleri.NakitKasa, true, cancellationToken);
        return items.Select(x => new HesapLookupDto { Id = x.Id, Kod = x.Kod, Ad = x.Ad }).ToList();
    }

    public async Task<List<HesapLookupDto>> GetBankaHesapLookupsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _kasaBankaHesapRepository.GetByTipAsync(KasaBankaHesapTipleri.Banka, true, cancellationToken);
        return items.Select(x => new HesapLookupDto { Id = x.Id, Kod = x.Kod, Ad = x.Ad }).ToList();
    }

    public async Task<List<HesapLookupDto>> GetDepoLookupsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _depoRepository.GetAllAsync();
        return items.Where(x => x.AktifMi).OrderBy(x => x.Kod).ThenBy(x => x.Ad)
            .Select(x => new HesapLookupDto { Id = x.Id, Kod = x.Kod, Ad = x.Ad }).ToList();
    }

    public async Task<List<HesapLookupDto>> GetMuhasebeKodLookupsAsync(string? startsWith, CancellationToken cancellationToken = default)
    {
        var prefix = startsWith?.Trim();
        var items = await _muhasebeHesapPlaniRepository.GetAllAsync();
        var query = items.Where(x => x.AktifMi);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            query = query.Where(x => x.TamKod.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderBy(x => x.TamKod).ThenBy(x => x.Ad)
            .Take(300)
            .Select(x => new HesapLookupDto { Id = x.Id, Kod = x.TamKod, Ad = x.Ad }).ToList();
    }

    public override async Task<HesapDto> AddAsync(HesapDto dto)
    {
        await ValidateAndNormalizeAsync(dto, null);
        var created = await base.AddAsync(dto);
        await SyncLinksAsync(created.Id!.Value, dto.KasaHesapIds, dto.BankaHesapIds, dto.DepoIds);
        return (await GetDetailByIdAsync(created.Id.Value))!;
    }

    public override async Task<HesapDto> UpdateAsync(HesapDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Hesap id zorunludur.", 400);
        }

        await ValidateAndNormalizeAsync(dto, dto.Id.Value);
        var updated = await base.UpdateAsync(dto);
        await SyncLinksAsync(updated.Id!.Value, dto.KasaHesapIds, dto.BankaHesapIds, dto.DepoIds);
        return (await GetDetailByIdAsync(updated.Id.Value))!;
    }

    public override async Task DeleteAsync(int id)
    {
        var kasaBankaLinks = await _dbContext.Set<HesapKasaBankaBaglanti>().Where(x => x.HesapId == id).ToListAsync();
        var depoLinks = await _dbContext.Set<HesapDepoBaglanti>().Where(x => x.HesapId == id).ToListAsync();

        if (kasaBankaLinks.Count > 0)
        {
            _dbContext.RemoveRange(kasaBankaLinks);
        }

        if (depoLinks.Count > 0)
        {
            _dbContext.RemoveRange(depoLinks);
        }

        await _dbContext.SaveChangesAsync();
        await base.DeleteAsync(id);
    }

    private async Task ValidateAndNormalizeAsync(HesapDto dto, int? currentId)
    {
        dto.Ad = (dto.Ad ?? string.Empty).Trim();
        dto.MuhasebeFormu = NormalizeOptional(dto.MuhasebeFormu, 64);
        dto.Aciklama = NormalizeOptional(dto.Aciklama, 1024);

        dto.KasaHesapIds = (dto.KasaHesapIds ?? []).Where(x => x > 0).Distinct().ToList();
        dto.BankaHesapIds = (dto.BankaHesapIds ?? []).Where(x => x > 0).Distinct().ToList();
        dto.DepoIds = (dto.DepoIds ?? []).Where(x => x > 0).Distinct().ToList();

        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Hesap adi zorunludur.", 400);
        }

        var duplicate = await _hesapRepository.ExistsByAdAsync(dto.Ad, currentId);
        if (duplicate)
        {
            throw new BaseException("Hesap adi benzersiz olmalidir.", 400);
        }

        var muhasebe = await _muhasebeHesapPlaniRepository.GetByIdAsync(dto.MuhasebeHesapPlaniId);
        if (muhasebe is null || !muhasebe.AktifMi)
        {
            throw new BaseException("Secilen muhasebe hesap plani kaydi bulunamadi veya pasif.", 400);
        }

        if (dto.KasaHesapIds.Count > 0)
        {
            var kasaSet = (await _kasaBankaHesapRepository.GetByTipAsync(KasaBankaHesapTipleri.NakitKasa, true)).Select(x => x.Id).ToHashSet();
            if (dto.KasaHesapIds.Any(x => !kasaSet.Contains(x)))
            {
                throw new BaseException("Secilen kasa hesaplarindan bazilari gecersiz.", 400);
            }
        }

        if (dto.BankaHesapIds.Count > 0)
        {
            var bankaSet = (await _kasaBankaHesapRepository.GetByTipAsync(KasaBankaHesapTipleri.Banka, true)).Select(x => x.Id).ToHashSet();
            if (dto.BankaHesapIds.Any(x => !bankaSet.Contains(x)))
            {
                throw new BaseException("Secilen banka hesaplarindan bazilari gecersiz.", 400);
            }
        }

        if (dto.DepoIds.Count > 0)
        {
            var depoSet = (await _depoRepository.GetAllAsync()).Where(x => x.AktifMi).Select(x => x.Id).ToHashSet();
            if (dto.DepoIds.Any(x => !depoSet.Contains(x)))
            {
                throw new BaseException("Secilen depolardan bazilari gecersiz.", 400);
            }
        }
    }

    private async Task SyncLinksAsync(int hesapId, List<int> kasaIds, List<int> bankaIds, List<int> depoIds)
    {
        var allKasaBankaIds = kasaIds.Concat(bankaIds).Distinct().ToHashSet();

        var existingKasaBanka = await _dbContext.Set<HesapKasaBankaBaglanti>()
            .Where(x => x.HesapId == hesapId)
            .ToListAsync();

        var removeKasaBanka = existingKasaBanka.Where(x => !allKasaBankaIds.Contains(x.KasaBankaHesapId)).ToList();
        if (removeKasaBanka.Count > 0)
        {
            _dbContext.RemoveRange(removeKasaBanka);
        }

        var existingKasaBankaIds = existingKasaBanka.Select(x => x.KasaBankaHesapId).ToHashSet();
        var addKasaBanka = allKasaBankaIds.Where(x => !existingKasaBankaIds.Contains(x))
            .Select(x => new HesapKasaBankaBaglanti { HesapId = hesapId, KasaBankaHesapId = x })
            .ToList();
        if (addKasaBanka.Count > 0)
        {
            await _dbContext.AddRangeAsync(addKasaBanka);
        }

        var existingDepo = await _dbContext.Set<HesapDepoBaglanti>()
            .Where(x => x.HesapId == hesapId)
            .ToListAsync();

        var depoSet = depoIds.ToHashSet();
        var removeDepo = existingDepo.Where(x => !depoSet.Contains(x.DepoId)).ToList();
        if (removeDepo.Count > 0)
        {
            _dbContext.RemoveRange(removeDepo);
        }

        var existingDepoIds = existingDepo.Select(x => x.DepoId).ToHashSet();
        var addDepo = depoSet.Where(x => !existingDepoIds.Contains(x))
            .Select(x => new HesapDepoBaglanti { HesapId = hesapId, DepoId = x })
            .ToList();
        if (addDepo.Count > 0)
        {
            await _dbContext.AddRangeAsync(addDepo);
        }

        await _dbContext.SaveChangesAsync();
    }

    private static string? NormalizeOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLen ? normalized : normalized[..maxLen];
    }

    private static HesapDto MapDetailDto(Hesap entity)
    {
        var dto = new HesapDto
        {
            Id = entity.Id,
            Ad = entity.Ad,
            MuhasebeHesapPlaniId = entity.MuhasebeHesapPlaniId,
            MuhasebeTamKod = entity.MuhasebeHesapPlani?.TamKod,
            MuhasebeHesapAdi = entity.MuhasebeHesapPlani?.Ad,
            GenelHesapMi = entity.GenelHesapMi,
            MuhasebeFormu = entity.MuhasebeFormu,
            AktifMi = entity.AktifMi,
            Aciklama = entity.Aciklama
        };

        foreach (var link in entity.KasaBankaBaglantilari)
        {
            if (link.KasaBankaHesap is null)
            {
                continue;
            }

            if (link.KasaBankaHesap.Tip == KasaBankaHesapTipleri.NakitKasa)
            {
                dto.KasaHesapIds.Add(link.KasaBankaHesapId);
            }
            else if (link.KasaBankaHesap.Tip == KasaBankaHesapTipleri.Banka)
            {
                dto.BankaHesapIds.Add(link.KasaBankaHesapId);
            }
        }

        dto.DepoIds = entity.DepoBaglantilari.Select(x => x.DepoId).Distinct().ToList();
        dto.KasaHesapIds = dto.KasaHesapIds.Distinct().ToList();
        dto.BankaHesapIds = dto.BankaHesapIds.Distinct().ToList();

        return dto;
    }
}
