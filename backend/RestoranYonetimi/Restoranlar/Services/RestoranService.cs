using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.Infrastructure.EntityFramework;
using STYS.IsletmeAlanlari.Entities;
using STYS.Restoranlar.Dtos;
using STYS.Restoranlar.Entities;
using STYS.Restoranlar.Repositories;
using STYS.RestoranYonetimi.Services;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Restoranlar.Services;

public class RestoranService : BaseRdbmsService<RestoranDto, Restoran, int>, IRestoranService
{
    private const string RestoranIsletmeAlaniSinifKodu = "RESTORAN";
    private const string KullaniciTipiDomain = "KullaniciTipi";
    private const string KullaniciTipiAdminRoleName = "Admin";
    private readonly StysAppDbContext _dbContext;
    private readonly IRestoranRepository _restoranRepository;
    private readonly IMapper _mapper;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IRestoranErisimService _restoranErisimService;

    public RestoranService(
        StysAppDbContext dbContext,
        IRestoranRepository restoranRepository,
        IMapper mapper,
        IUserRepository userRepository,
        IUserService userService,
        TodIdentityDbContext identityDbContext,
        ICurrentUserAccessor currentUserAccessor,
        IRestoranErisimService restoranErisimService)
        : base(restoranRepository, mapper)
    {
        _dbContext = dbContext;
        _restoranRepository = restoranRepository;
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

        var restoran = await _restoranRepository.GetByIdAsync(restoranId)
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

        var restoran = await _restoranRepository.GetByIdAsync(restoranId)
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

    public override async Task<IEnumerable<RestoranDto>> GetAllAsync(Func<IQueryable<Restoran>, IQueryable<Restoran>>? include = null)
    {
        var query = await _restoranErisimService.ApplyRestoranScopeAsync(_restoranRepository.Where(x => true));
        query = BuildIncludeQuery(query, include);
        var entities = await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return MapRestoranDtos(entities);
    }

    public override async Task<IEnumerable<RestoranDto>> WhereAsync(
        Expression<Func<Restoran, bool>> predicate,
        Func<IQueryable<Restoran>, IQueryable<Restoran>>? include = null)
    {
        var query = await _restoranErisimService.ApplyRestoranScopeAsync(_restoranRepository.Where(predicate));
        query = BuildIncludeQuery(query, include);
        var entities = await query
            .OrderBy(x => x.Ad)
            .ThenBy(x => x.Id)
            .ToListAsync();

        return MapRestoranDtos(entities);
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

    public override async Task<RestoranDto?> GetByIdAsync(int id, Func<IQueryable<Restoran>, IQueryable<Restoran>>? include = null)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(id);

        var entity = await _restoranRepository.GetByIdAsync(id, query => BuildIncludeQuery(query, include));

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

    public override async Task<RestoranDto> AddAsync(RestoranDto request)
    {
        Validate(request.TesisId, request.Ad!);

        var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == request.TesisId && x.AktifMi);
        if (!tesisExists)
        {
            throw new BaseException("Gecerli ve aktif tesis bulunamadi.", 400);
        }
        await ValidateIsletmeAlaniSecimiAsync(request.TesisId, request.IsletmeAlaniId, CancellationToken.None);
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, preserveWhenNull: false, CancellationToken.None);
        var garsonUserIds = await NormalizeAndValidateGarsonIdsAsync(request.GarsonUserIds, preserveWhenNull: false, CancellationToken.None);

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _restoranRepository.AnyAsync(x => x.TesisId == request.TesisId && x.Ad.ToUpper() == normalizedAd && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni adla aktif restoran zaten var.", 400);
        }

        request.Ad = request.Ad.Trim();
        request.Aciklama = NormalizeOptional(request.Aciklama, 512);

        var created = await base.AddAsync(request);
        var entity = await _restoranRepository.GetByIdAsync(created.Id!.Value, q => q.Include(x => x.Yoneticiler).Include(x => x.Garsonlar))
            ?? throw new BaseException("Olusturulan restoran bulunamadi.", 500);

        SyncYoneticiler(entity, yoneticiUserIds ?? []);
        SyncGarsonlar(entity, garsonUserIds ?? []);
        _restoranRepository.Update(entity);
        await _restoranRepository.SaveChangesAsync();

        return (await GetByIdAsync(entity.Id))!;
    }

    public override async Task<RestoranDto> UpdateAsync(RestoranDto request)
    {
        if (!request.Id.HasValue)
        {
            throw new BaseException("Restoran id zorunludur.", 400);
        }

        Validate(request.TesisId, request.Ad!);
        await _restoranErisimService.EnsureRestoranErisimiAsync(request.Id.Value);

        var entity = await _restoranRepository.GetByIdAsync(request.Id.Value, query => query
            .Include(x => x.Yoneticiler)
            .Include(x => x.Garsonlar))
            ?? throw new BaseException("Restoran bulunamadi.", 404);

        var tesisExists = await _dbContext.Tesisler.AnyAsync(x => x.Id == request.TesisId && x.AktifMi);
        if (!tesisExists)
        {
            throw new BaseException("Gecerli ve aktif tesis bulunamadi.", 400);
        }
        await ValidateIsletmeAlaniSecimiAsync(request.TesisId, request.IsletmeAlaniId, CancellationToken.None);
        var yoneticiUserIds = await NormalizeAndValidateManagerIdsAsync(request.YoneticiUserIds, preserveWhenNull: true, CancellationToken.None);
        var garsonUserIds = await NormalizeAndValidateGarsonIdsAsync(request.GarsonUserIds, preserveWhenNull: true, CancellationToken.None);

        var normalizedAd = request.Ad.Trim().ToUpperInvariant();
        var exists = await _restoranRepository.AnyAsync(x => x.Id != request.Id.Value && x.TesisId == request.TesisId && x.Ad.ToUpper() == normalizedAd && x.AktifMi);
        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni adla aktif restoran zaten var.", 400);
        }

        request.Ad = request.Ad.Trim();
        request.Aciklama = NormalizeOptional(request.Aciklama, 512);

        var updated = await base.UpdateAsync(request);
        entity = await _restoranRepository.GetByIdAsync(updated.Id!.Value, query => query
            .Include(x => x.Yoneticiler)
            .Include(x => x.Garsonlar))
            ?? throw new BaseException("Guncellenen restoran bulunamadi.", 500);

        if (yoneticiUserIds is not null)
        {
            SyncYoneticiler(entity, yoneticiUserIds);
        }
        if (garsonUserIds is not null)
        {
            SyncGarsonlar(entity, garsonUserIds);
        }

        _restoranRepository.Update(entity);
        await _restoranRepository.SaveChangesAsync();
        return await GetByIdAsync(entity.Id) ?? _mapper.Map<RestoranDto>(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        await _restoranErisimService.EnsureRestoranErisimiAsync(id);
        await base.DeleteAsync(id);
    }

    private IQueryable<Restoran> BuildIncludeQuery(
        IQueryable<Restoran> query,
        Func<IQueryable<Restoran>, IQueryable<Restoran>>? include = null)
    {
        var included = query
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.Bina)
            .Include(x => x.IsletmeAlani)
            .ThenInclude(x => x!.IsletmeAlaniSinifi)
            .Include(x => x.Yoneticiler)
            .Include(x => x.Garsonlar);

        return include is null ? included : include(included);
    }

    private List<RestoranDto> MapRestoranDtos(List<Restoran> entities)
    {
        var dtos = _mapper.Map<List<RestoranDto>>(entities);
        var dtoMap = dtos
            .Where(x => x.Id.HasValue)
            .ToDictionary(x => x.Id!.Value);

        foreach (var entity in entities)
        {
            if (!dtoMap.TryGetValue(entity.Id, out var dto))
            {
                continue;
            }

            dto.IsletmeAlaniAdi = BuildIsletmeAlaniAdi(entity.IsletmeAlani);
            dto.YoneticiUserIds = entity.Yoneticiler.Select(x => x.UserId).Distinct().ToList();
            dto.GarsonUserIds = entity.Garsonlar.Select(x => x.UserId).Distinct().ToList();
        }

        return dtos;
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
