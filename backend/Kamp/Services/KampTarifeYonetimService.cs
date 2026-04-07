using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Kamp.Services;

public class KampTarifeYonetimService : IKampTarifeYonetimService
{
    private readonly StysAppDbContext _dbContext;

    public KampTarifeYonetimService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<KampTarifeYonetimBaglamDto> GetBaglamAsync(CancellationToken cancellationToken = default)
    {
        var programlar = await _dbContext.KampProgramlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Ad)
            .Select(x => new KampProgramiSecenekDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new KampTarifeYonetimBaglamDto
        {
            Programlar = programlar
        };
    }

    public async Task<List<KampKonaklamaTarifeYonetimDto>> GetTarifelerAsync(int kampProgramiId, CancellationToken cancellationToken = default)
    {
        var tarifeler = await _dbContext.KampKonaklamaTarifeleri
            .AsNoTracking()
            .Where(x => x.KampProgramiId == kampProgramiId && !x.IsDeleted)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Kod)
            .Select(x => new KampKonaklamaTarifeYonetimDto
            {
                Id = x.Id,
                KampProgramiId = x.KampProgramiId,
                Kod = x.Kod,
                Ad = x.Ad,
                MinimumKisi = x.MinimumKisi,
                MaksimumKisi = x.MaksimumKisi,
                KamuGunlukUcret = x.KamuGunlukUcret,
                DigerGunlukUcret = x.DigerGunlukUcret,
                BuzdolabiGunlukUcret = x.BuzdolabiGunlukUcret,
                TelevizyonGunlukUcret = x.TelevizyonGunlukUcret,
                KlimaGunlukUcret = x.KlimaGunlukUcret,
                AktifMi = x.AktifMi
            })
            .ToListAsync(cancellationToken);

        return tarifeler;
    }

    public async Task<List<KampKonaklamaTarifeYonetimDto>> KaydetAsync(int kampProgramiId, KampTarifeKaydetRequestDto request, CancellationToken cancellationToken = default)
    {
        // Program var mı kontrol et
        var program = await _dbContext.KampProgramlari
            .FirstOrDefaultAsync(x => x.Id == kampProgramiId && !x.IsDeleted, cancellationToken)
            ?? throw new BaseException("Kamp programi bulunamadi.", 404);

        // Mevcut tarifeleri yükle
        var existing = await _dbContext.KampKonaklamaTarifeleri
            .Where(x => x.KampProgramiId == kampProgramiId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var requestByIdMap = (request.Tarifeler ?? [])
            .Where(x => x.Id.HasValue && x.Id > 0)
            .ToDictionary(x => x.Id.Value);

        // Update veya Insert
        foreach (var dto in request.Tarifeler ?? [])
        {
            if (dto.Id.HasValue && dto.Id > 0 && existing.TryGetValue(dto.Id.Value, out var entity))
            {
                // Update
                entity.Kod = dto.Kod?.Trim() ?? string.Empty;
                entity.Ad = dto.Ad?.Trim() ?? string.Empty;
                entity.MinimumKisi = dto.MinimumKisi;
                entity.MaksimumKisi = dto.MaksimumKisi;
                entity.KamuGunlukUcret = dto.KamuGunlukUcret;
                entity.DigerGunlukUcret = dto.DigerGunlukUcret;
                entity.BuzdolabiGunlukUcret = dto.BuzdolabiGunlukUcret;
                entity.TelevizyonGunlukUcret = dto.TelevizyonGunlukUcret;
                entity.KlimaGunlukUcret = dto.KlimaGunlukUcret;
                entity.AktifMi = dto.AktifMi;
                entity.IsDeleted = false;
            }
            else
            {
                // Insert
                await _dbContext.KampKonaklamaTarifeleri.AddAsync(
                    new KampKonaklamaTarifesi
                    {
                        KampProgramiId = kampProgramiId,
                        Kod = dto.Kod?.Trim() ?? string.Empty,
                        Ad = dto.Ad?.Trim() ?? string.Empty,
                        MinimumKisi = dto.MinimumKisi,
                        MaksimumKisi = dto.MaksimumKisi,
                        KamuGunlukUcret = dto.KamuGunlukUcret,
                        DigerGunlukUcret = dto.DigerGunlukUcret,
                        BuzdolabiGunlukUcret = dto.BuzdolabiGunlukUcret,
                        TelevizyonGunlukUcret = dto.TelevizyonGunlukUcret,
                        KlimaGunlukUcret = dto.KlimaGunlukUcret,
                        AktifMi = dto.AktifMi
                    },
                    cancellationToken);
            }
        }

        // Soft-delete: DTO'da olmayan mevcut kayıtlar
        foreach (var (id, entity) in existing)
        {
            if (!requestByIdMap.ContainsKey(id))
            {
                entity.IsDeleted = true;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetTarifelerAsync(kampProgramiId, cancellationToken);
    }
}
