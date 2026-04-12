using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Restoranlar.Services;

public class RestoranService : IRestoranService
{
    private const string RestoranIsletmeAlaniSinifKodu = "RESTORAN";
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;

    public RestoranService(StysAppDbContext dbContext, IMapper mapper, IUserRepository userRepository)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _userRepository = userRepository;
    }

    public async Task<List<RestoranDto>> GetListAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Restoranlar.AsQueryable();
        if (tesisId.HasValue && tesisId.Value > 0)
        {
            query = query.Where(x => x.TesisId == tesisId.Value);
        }

        var entities = await query
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.Bina)
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.IsletmeAlaniSinifi)
            .Include(x => x.Yoneticiler)
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<RestoranDto>>(entities);
        var dtoMap = dtos
            .Where(x => x.Id.HasValue)
            .ToDictionary(x => x.Id!.Value);
        foreach (var entity in entities)
        {
            if (dtoMap.TryGetValue(entity.Id, out var dto))
            {
                dto.IsletmeAlaniAdi = BuildIsletmeAlaniAdi(entity.IsletmeAlani);
                dto.YoneticiUserIds = entity.Yoneticiler
                    .Select(x => x.UserId)
                    .Distinct()
                    .ToList();
            }
        }

        return dtos;
    }

    public async Task<List<RestoranIsletmeAlaniSecenekDto>> GetIsletmeAlaniSecenekleriAsync(int tesisId, CancellationToken cancellationToken = default)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Gecerli tesis secimi zorunludur.", 400);
        }

        var rawItems = await _dbContext.IsletmeAlanlari
            .Where(x =>
                x.AktifMi &&
                x.Bina != null &&
                x.Bina.TesisId == tesisId &&
                x.Bina.AktifMi &&
                x.IsletmeAlaniSinifi != null &&
                x.IsletmeAlaniSinifi.AktifMi &&
                x.IsletmeAlaniSinifi.Kod == RestoranIsletmeAlaniSinifKodu)
            .Select(x => new
            {
                Id = x.Id,
                x.OzelAd,
                BinaAdi = x.Bina!.Ad,
                SinifAdi = x.IsletmeAlaniSinifi!.Ad
            })
            .ToListAsync(cancellationToken);

        return rawItems
            .Select(x => new RestoranIsletmeAlaniSecenekDto
            {
                Id = x.Id,
                Ad = !string.IsNullOrWhiteSpace(x.OzelAd)
                    ? x.OzelAd!.Trim()
                    : $"{x.BinaAdi} / {x.SinifAdi}"
            })
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToList();
    }

    public async Task<RestoranDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Restoranlar
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.Bina)
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.IsletmeAlaniSinifi)
            .Include(x => x.Yoneticiler)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var dto = _mapper.Map<RestoranDto>(entity);
        dto.IsletmeAlaniAdi = BuildIsletmeAlaniAdi(entity.IsletmeAlani);
        dto.YoneticiUserIds = entity.Yoneticiler
            .Select(x => x.UserId)
            .Distinct()
            .ToList();
        return dto;
    }

    public async Task<RestoranDto> CreateAsync(CreateRestoranRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.TesisId, request.Ad);

        var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == request.TesisId && x.AktifMi, cancellationToken);
        if (!tesisExists)
        {
            throw new BaseException("Gecerli ve aktif tesis bulunamadi.", 400);
        }
        await ValidateIsletmeAlaniSecimiAsync(request.TesisId, request.IsletmeAlaniId, cancellationToken);
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, cancellationToken);

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _dbContext.Restoranlar.AnyAsync(x => x.TesisId == request.TesisId && x.Ad.ToUpper() == normalizedAd && x.AktifMi, cancellationToken);
        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni adla aktif restoran zaten var.", 400);
        }

        var entity = new Restoran
        {
            TesisId = request.TesisId,
            IsletmeAlaniId = request.IsletmeAlaniId,
            Ad = request.Ad.Trim(),
            Aciklama = NormalizeOptional(request.Aciklama, 512),
            AktifMi = request.AktifMi,
            Yoneticiler = yoneticiUserIds
                .Select(x => new RestoranYonetici
                {
                    UserId = x
                })
                .ToList()
        };

        _dbContext.Restoranlar.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? _mapper.Map<RestoranDto>(entity);
    }

    public async Task<RestoranDto> UpdateAsync(int id, UpdateRestoranRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.TesisId, request.Ad);

        var entity = await _dbContext.Restoranlar.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Restoran bulunamadi.", 404);
        await _dbContext.Entry(entity)
            .Collection(x => x.Yoneticiler)
            .LoadAsync(cancellationToken);

        var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == request.TesisId && x.AktifMi, cancellationToken);
        if (!tesisExists)
        {
            throw new BaseException("Gecerli ve aktif tesis bulunamadi.", 400);
        }
        await ValidateIsletmeAlaniSecimiAsync(request.TesisId, request.IsletmeAlaniId, cancellationToken);
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, cancellationToken);

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _dbContext.Restoranlar.AnyAsync(x => x.Id != id && x.TesisId == request.TesisId && x.Ad.ToUpper() == normalizedAd && x.AktifMi, cancellationToken);
        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni adla aktif restoran zaten var.", 400);
        }

        entity.TesisId = request.TesisId;
        entity.IsletmeAlaniId = request.IsletmeAlaniId;
        entity.Ad = request.Ad.Trim();
        entity.Aciklama = NormalizeOptional(request.Aciklama, 512);
        entity.AktifMi = request.AktifMi;
        SyncYoneticiler(entity, yoneticiUserIds);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? _mapper.Map<RestoranDto>(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Restoranlar.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Restoran bulunamadi.", 404);

        _dbContext.Restoranlar.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(int tesisId, string ad)
    {
        if (tesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(ad))
        {
            throw new BaseException("Restoran adi zorunludur.", 400);
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private async Task ValidateIsletmeAlaniSecimiAsync(int tesisId, int? isletmeAlaniId, CancellationToken cancellationToken)
    {
        if (!isletmeAlaniId.HasValue)
        {
            return;
        }

        var secilenAlanGecerli = await _dbContext.IsletmeAlanlari.AnyAsync(x =>
            x.Id == isletmeAlaniId.Value &&
            x.AktifMi &&
            x.Bina != null &&
            x.Bina.AktifMi &&
            x.Bina.TesisId == tesisId &&
            x.IsletmeAlaniSinifi != null &&
            x.IsletmeAlaniSinifi.AktifMi &&
            x.IsletmeAlaniSinifi.Kod == RestoranIsletmeAlaniSinifKodu, cancellationToken);

        if (!secilenAlanGecerli)
        {
            throw new BaseException("Secilen isletme alani restoran sinifinda degil veya tesisle uyumlu degil.", 400);
        }
    }

    private static string? BuildIsletmeAlaniAdi(IsletmeAlani? isletmeAlani)
    {
        if (isletmeAlani is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(isletmeAlani.OzelAd))
        {
            return isletmeAlani.OzelAd!.Trim();
        }

        var binaAdi = isletmeAlani.Bina?.Ad;
        var sinifAdi = isletmeAlani.IsletmeAlaniSinifi?.Ad;
        if (!string.IsNullOrWhiteSpace(binaAdi) && !string.IsNullOrWhiteSpace(sinifAdi))
        {
            return $"{binaAdi} / {sinifAdi}";
        }

        return sinifAdi ?? binaAdi;
    }

    private async Task<List<Guid>> NormalizeAndValidateManagerIdsAsync(
        ICollection<Guid>? managerUserIds,
        CancellationToken cancellationToken)
    {
        if (managerUserIds is null)
        {
            return [];
        }

        var normalizedManagerIds = managerUserIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedManagerIds.Count == 0)
        {
            return [];
        }

        var existingUserIds = await _userRepository
            .Where(x => normalizedManagerIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var missingUserIds = normalizedManagerIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
        {
            throw new BaseException("Secilen restoran yoneticilerinden en az biri bulunamadi.", 400);
        }

        return normalizedManagerIds;
    }

    private void SyncYoneticiler(Restoran entity, IReadOnlyCollection<Guid> managerUserIds)
    {
        entity.Yoneticiler ??= [];

        var byUserId = entity.Yoneticiler.ToDictionary(x => x.UserId);
        var desiredUserIds = managerUserIds.ToHashSet();

        var toDelete = entity.Yoneticiler
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _dbContext.RestoranYoneticileri.RemoveRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Yoneticiler.Add(new RestoranYonetici
            {
                UserId = desiredUserId
            });
        }
    }
}
