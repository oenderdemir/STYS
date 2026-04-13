using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Entities;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Restoranlar.Services;

public class RestoranService : IRestoranService
{
    private const string RestoranIsletmeAlaniSinifKodu = "RESTORAN";
    private const string KullaniciTipiDomain = "KullaniciTipi";
    private const string KullaniciTipiAdminRoleName = "Admin";
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranService(
        StysAppDbContext dbContext,
        IMapper mapper,
        IUserRepository userRepository,
        IUserService userService,
        TodIdentityDbContext identityDbContext,
        ICurrentUserAccessor currentUserAccessor,
        IRestoranErisimService restoranErisimService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _userRepository = userRepository;
        _userService = userService;
        _identityDbContext = identityDbContext;
        _currentUserAccessor = currentUserAccessor;
        _restoranErisimService = restoranErisimService;
    }

    public async Task<UserDto> CreateRestoranYoneticisiUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default)
    {
        if (dto is null)
        {
            throw new BaseException("Kullanici bilgisi zorunludur.", 400);
        }

        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);
        await EnsureCurrentUserHasPermissionAsync(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtayabilir, cancellationToken);

        var restoran = await _dbContext.Restoranlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restoranId, cancellationToken)
            ?? throw new BaseException("Secilen restoran bulunamadi.", 404);

        var groupId = await GetGroupIdByMarkerAsync(nameof(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir), cancellationToken);
        if (groupId == Guid.Empty)
        {
            throw new BaseException("Restoran yoneticisi grubu bulunamadi.", 400);
        }

        dto.UserGroups =
        [
            new UserGroupDto
            {
                Id = groupId
            }
        ];

        var created = await _userService.AddAsync(dto);
        if (!created.Id.HasValue)
        {
            throw new BaseException("Restoran yoneticisi olusturulurken kullanici kimligi alinamadi.", 500);
        }

        await SetOwnerTesisForCreatedUserAsync(created.Id.Value, restoran.TesisId, cancellationToken);
        await UpsertRestoranYoneticiAtamasiAsync(restoranId, created.Id.Value, cancellationToken);
        return created;
    }

    public async Task<UserDto> CreateRestoranGarsonuUserAsync(int restoranId, UserDto dto, CancellationToken cancellationToken = default)
    {
        if (dto is null)
        {
            throw new BaseException("Kullanici bilgisi zorunludur.", 400);
        }

        await _restoranErisimService.EnsureRestoranErisimiAsync(restoranId, cancellationToken);
        await EnsureCurrentUserHasPermissionAsync(StructurePermissions.KullaniciAtama.RestoranGarsonuAtayabilir, cancellationToken);

        var restoran = await _dbContext.Restoranlar
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restoranId, cancellationToken)
            ?? throw new BaseException("Secilen restoran bulunamadi.", 404);

        var groupId = await GetGroupIdByMarkerAsync(nameof(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir), cancellationToken);
        if (groupId == Guid.Empty)
        {
            throw new BaseException("Restoran garsonu grubu bulunamadi.", 400);
        }

        dto.UserGroups =
        [
            new UserGroupDto
            {
                Id = groupId
            }
        ];

        var created = await _userService.AddAsync(dto);
        if (!created.Id.HasValue)
        {
            throw new BaseException("Garson olusturulurken kullanici kimligi alinamadi.", 500);
        }

        await SetOwnerTesisForCreatedUserAsync(created.Id.Value, restoran.TesisId, cancellationToken);
        await UpsertRestoranGarsonAtamasiAsync(restoranId, created.Id.Value, cancellationToken);
        return created;
    }

    public async Task<List<RestoranDto>> GetListAsync(int? tesisId, CancellationToken cancellationToken = default)
    {
        var query = await _restoranErisimService.ApplyRestoranScopeAsync(_dbContext.Restoranlar.AsQueryable(), cancellationToken);
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
            .Include(x => x.Garsonlar)
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
                dto.GarsonUserIds = entity.Garsonlar
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
        await _restoranErisimService.EnsureRestoranErisimiAsync(id, cancellationToken);

        var entity = await _dbContext.Restoranlar
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.Bina)
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.IsletmeAlaniSinifi)
            .Include(x => x.Yoneticiler)
            .Include(x => x.Garsonlar)
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
        dto.GarsonUserIds = entity.Garsonlar
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
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, preserveWhenNull: false, cancellationToken);
        var garsonUserIds = await NormalizeAndValidateGarsonIdsAsync(request.GarsonUserIds, preserveWhenNull: false, cancellationToken);

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
            Yoneticiler = (yoneticiUserIds ?? [])
                .Select(x => new RestoranYonetici
                {
                    UserId = x
                })
                .ToList(),
            Garsonlar = (garsonUserIds ?? [])
                .Select(x => new RestoranGarson
                {
                    UserId = x
                })
                .ToList()
        };

        _dbContext.Restoranlar.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<RestoranDto>(entity);
        dto.YoneticiUserIds = entity.Yoneticiler.Select(x => x.UserId).Distinct().ToList();
        dto.GarsonUserIds = entity.Garsonlar.Select(x => x.UserId).Distinct().ToList();
        return dto;
    }

    public async Task<RestoranDto> UpdateAsync(int id, UpdateRestoranRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request.TesisId, request.Ad);
        await _restoranErisimService.EnsureRestoranErisimiAsync(id, cancellationToken);

        var entity = await _dbContext.Restoranlar.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new BaseException("Restoran bulunamadi.", 404);
        await _dbContext.Entry(entity)
            .Collection(x => x.Yoneticiler)
            .LoadAsync(cancellationToken);
        await _dbContext.Entry(entity)
            .Collection(x => x.Garsonlar)
            .LoadAsync(cancellationToken);

        var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == request.TesisId && x.AktifMi, cancellationToken);
        if (!tesisExists)
        {
            throw new BaseException("Gecerli ve aktif tesis bulunamadi.", 400);
        }
        await ValidateIsletmeAlaniSecimiAsync(request.TesisId, request.IsletmeAlaniId, cancellationToken);
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, preserveWhenNull: true, cancellationToken);
        var garsonUserIds = await NormalizeAndValidateGarsonIdsAsync(request.GarsonUserIds, preserveWhenNull: true, cancellationToken);

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
        if (yoneticiUserIds is not null)
        {
            SyncYoneticiler(entity, yoneticiUserIds);
        }
        if (garsonUserIds is not null)
        {
            SyncGarsonlar(entity, garsonUserIds);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken) ?? _mapper.Map<RestoranDto>(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(id, cancellationToken);

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

    private async Task<List<Guid>?> NormalizeAndValidateManagerIdsAsync(
        ICollection<Guid>? managerUserIds,
        bool preserveWhenNull,
        CancellationToken cancellationToken)
    {
        await EnsureCanAssignForPayloadAsync(
            managerUserIds,
            StructurePermissions.KullaniciAtama.RestoranYoneticisiAtayabilir,
            preserveWhenNull);

        if (managerUserIds is null)
        {
            return preserveWhenNull ? null : [];
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

        var restoranYoneticiUserIds = await GetUsersMatchingMarkerAsync(
            normalizedManagerIds,
            nameof(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir),
            cancellationToken);

        var invalidGroupUserIds = normalizedManagerIds.Except(restoranYoneticiUserIds).ToList();
        if (invalidGroupUserIds.Count > 0)
        {
            throw new BaseException("Secilen kullanicilar restoran yoneticisi atanabilir bir grupta olmalidir.", 400);
        }

        return normalizedManagerIds;
    }

    private async Task<List<Guid>?> NormalizeAndValidateGarsonIdsAsync(
        ICollection<Guid>? garsonUserIds,
        bool preserveWhenNull,
        CancellationToken cancellationToken)
    {
        await EnsureCanAssignForPayloadAsync(
            garsonUserIds,
            StructurePermissions.KullaniciAtama.RestoranGarsonuAtayabilir,
            preserveWhenNull);

        if (garsonUserIds is null)
        {
            return preserveWhenNull ? null : [];
        }

        var normalizedGarsonIds = garsonUserIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedGarsonIds.Count == 0)
        {
            return [];
        }

        var existingUserIds = await _userRepository
            .Where(x => normalizedGarsonIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var missingUserIds = normalizedGarsonIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
        {
            throw new BaseException("Secilen garsonlardan en az biri bulunamadi.", 400);
        }

        var garsonMarkerUserIds = await GetUsersMatchingMarkerAsync(
            normalizedGarsonIds,
            nameof(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir),
            cancellationToken);

        var invalidGroupUserIds = normalizedGarsonIds.Except(garsonMarkerUserIds).ToList();
        if (invalidGroupUserIds.Count > 0)
        {
            throw new BaseException("Secilen kullanicilar restoran garsonu atanabilir bir grupta olmalidir.", 400);
        }

        return normalizedGarsonIds;
    }

    private async Task EnsureCanAssignForPayloadAsync(
        ICollection<Guid>? userIds,
        string requiredPermission,
        bool preserveWhenNull = false)
    {
        if (userIds is null)
        {
            if (preserveWhenNull)
            {
                return;
            }

            return;
        }

        await EnsureCurrentUserHasPermissionAsync(requiredPermission);
    }

    private async Task EnsureCurrentUserHasPermissionAsync(string requiredPermission, CancellationToken cancellationToken = default)
    {
        var permissionSet = await GetCurrentUserPermissionSetAsync(cancellationToken);
        if (permissionSet.Contains(requiredPermission))
        {
            return;
        }

        throw new BaseException("Bu islem icin gerekli kullanici atama yetkiniz bulunmuyor.", 403);
    }

    private async Task<Guid> GetGroupIdByMarkerAsync(string markerRoleName, CancellationToken cancellationToken = default)
    {
        var query = _identityDbContext.UserGroups
            .Where(x => x.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == nameof(StructurePermissions.KullaniciAtama)
                && ugr.Role.Name == markerRoleName))
            .Where(x => !x.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == KullaniciTipiDomain
                && ugr.Role.Name == KullaniciTipiAdminRoleName));

        if (markerRoleName == nameof(StructurePermissions.KullaniciAtama.RestoranYoneticisiAtanabilir))
        {
            query = query.OrderByDescending(x => x.Name == "RestoranYoneticiGrubu");
        }
        else if (markerRoleName == nameof(StructurePermissions.KullaniciAtama.RestoranGarsonuAtanabilir))
        {
            query = query.OrderByDescending(x => x.Name == "GarsonGrubu");
        }

        return await query
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }


    private async Task SetOwnerTesisForCreatedUserAsync(Guid userId, int tesisId, CancellationToken cancellationToken)
    {
        var existingOwner = await _dbContext.KullaniciTesisSahiplikleri
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existingOwner is null)
        {
            await _dbContext.KullaniciTesisSahiplikleri.AddAsync(new()
            {
                UserId = userId,
                TesisId = tesisId
            }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (existingOwner.TesisId == tesisId)
        {
            return;
        }

        existingOwner.TesisId = tesisId;
        _dbContext.KullaniciTesisSahiplikleri.Update(existingOwner);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertRestoranYoneticiAtamasiAsync(int restoranId, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RestoranYoneticileri
            .FirstOrDefaultAsync(x => x.RestoranId == restoranId && x.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await _dbContext.RestoranYoneticileri.AddAsync(new RestoranYonetici
        {
            RestoranId = restoranId,
            UserId = userId
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertRestoranGarsonAtamasiAsync(int restoranId, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.RestoranGarsonlari
            .FirstOrDefaultAsync(x => x.RestoranId == restoranId && x.UserId == userId, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await _dbContext.RestoranGarsonlari.AddAsync(new RestoranGarson
        {
            RestoranId = restoranId,
            UserId = userId
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<string>> GetCurrentUserPermissionSetAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserAccessor.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _identityDbContext.UserUserGroups
            .Where(x => x.UserId == userId.Value)
            .SelectMany(x => x.UserGroup.UserGroupRoles.Select(ugr => new { ugr.Role.Domain, ugr.Role.Name }))
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => $"{x.Domain}.{x.Name}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<List<Guid>> GetUsersMatchingMarkerAsync(
        IReadOnlyCollection<Guid> userIds,
        string markerRoleName,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await _identityDbContext.UserUserGroups
            .Where(x => userIds.Contains(x.UserId))
            .Where(x => x.UserGroup.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == nameof(StructurePermissions.KullaniciAtama)
                && ugr.Role.Name == markerRoleName))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
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

    private void SyncGarsonlar(Restoran entity, IReadOnlyCollection<Guid> garsonUserIds)
    {
        entity.Garsonlar ??= [];

        var byUserId = entity.Garsonlar.ToDictionary(x => x.UserId);
        var desiredUserIds = garsonUserIds.ToHashSet();

        var toDelete = entity.Garsonlar
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _dbContext.RestoranGarsonlari.RemoveRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Garsonlar.Add(new RestoranGarson
            {
                UserId = desiredUserId
            });
        }
    }
}
