using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Repositories;
using STYS.Tesisler.Dto;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Repositories;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tesisler.Services;

public class TesisService : BaseRdbmsService<TesisDto, Tesis, int>, ITesisService
{
    private readonly ITesisRepository _tesisRepository;
    private readonly ITesisYoneticiRepository _tesisYoneticiRepository;
    private readonly ITesisResepsiyonistRepository _tesisResepsiyonistRepository;
    private readonly IIlRepository _ilRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly StysAppDbContext _stysDbContext;
    private readonly IUserAccessScopeService _userAccessScopeService;

    public TesisService(
        ITesisRepository tesisRepository,
        ITesisYoneticiRepository tesisYoneticiRepository,
        ITesisResepsiyonistRepository tesisResepsiyonistRepository,
        IIlRepository ilRepository,
        IUserRepository userRepository,
        IUserService userService,
        TodIdentityDbContext identityDbContext,
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(tesisRepository, mapper)
    {
        _tesisRepository = tesisRepository;
        _tesisYoneticiRepository = tesisYoneticiRepository;
        _tesisResepsiyonistRepository = tesisResepsiyonistRepository;
        _ilRepository = ilRepository;
        _userRepository = userRepository;
        _userService = userService;
        _identityDbContext = identityDbContext;
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public async Task<UserDto> CreateResepsiyonistUserAsync(int tesisId, UserDto dto)
    {
        if (dto is null)
        {
            throw new BaseException("Kullanici bilgisi zorunludur.", 400);
        }

        await EnsureCanAccessTesisAsync(tesisId);

        var tesis = await _tesisRepository.GetByIdAsync(tesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 404);
        }

        var receptionistGroupId = await _identityDbContext.UserGroups
            .Where(x => x.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == nameof(StructurePermissions.KullaniciGrupTipi)
                && ugr.Role.Name == nameof(StructurePermissions.KullaniciGrupTipi.Resepsiyonist)))
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (receptionistGroupId == Guid.Empty)
        {
            throw new BaseException("Resepsiyonist grubu bulunamadi.", 400);
        }

        dto.UserGroups =
        [
            new UserGroupDto
            {
                Id = receptionistGroupId
            }
        ];

        var created = await _userService.AddAsync(dto);
        if (!created.Id.HasValue)
        {
            throw new BaseException("Resepsiyonist olusturulurken kullanici kimligi alinamadi.", 500);
        }

        await SyncUserToSingleTesisAsync(created.Id.Value, tesisId);
        await EnsureOwnerRecordExistsAsync(created.Id.Value, tesisId);
        return created;
    }

    public override async Task<TesisDto> AddAsync(TesisDto dto)
    {
        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, null);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: false);
        var receptionistIds = await NormalizeAndValidateReceptionistIdsAsync(dto.ResepsiyonistUserIds, preserveWhenNull: false);

        var entity = Mapper.Map<Tesis>(dto);
        entity.Yoneticiler = managerIds!
            .Select(x => new TesisYonetici
            {
                UserId = x
            })
            .ToList();
        entity.Resepsiyonistler = receptionistIds!
            .Select(x => new TesisResepsiyonist
            {
                UserId = x
            })
            .ToList();

        await _tesisRepository.AddAsync(entity);
        await _tesisRepository.SaveChangesAsync();
        await EnsureOwnerRecordsForAssignedUsersAsync(entity.Id, managerIds!);
        await EnsureOwnerRecordsForAssignedUsersAsync(entity.Id, receptionistIds!);

        return Mapper.Map<TesisDto>(entity);
    }

    public override async Task<TesisDto> UpdateAsync(TesisDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Tesis id zorunludur.", 400);
        }

        var existingEntity = await _tesisRepository.GetByIdAsync(dto.Id.Value, query => query
            .Include(x => x.Yoneticiler)
            .Include(x => x.Resepsiyonistler));
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek tesis bulunamadi.", 404);
        }

        await EnsureCanAccessTesisAsync(existingEntity.Id);
        Normalize(dto);
        await EnsureIlRulesAsync(dto.IlId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: true);
        var receptionistIds = await NormalizeAndValidateReceptionistIdsAsync(dto.ResepsiyonistUserIds, preserveWhenNull: true);

        existingEntity.IsDeleted = false;
        existingEntity.Ad = dto.Ad;
        existingEntity.IlId = dto.IlId;
        existingEntity.Telefon = dto.Telefon;
        existingEntity.Adres = dto.Adres;
        existingEntity.Eposta = dto.Eposta;
        existingEntity.AktifMi = dto.AktifMi;

        List<Guid> previousManagerUserIds = [];
        if (managerIds is not null)
        {
            previousManagerUserIds = existingEntity.Yoneticiler
                .Select(x => x.UserId)
                .Distinct()
                .ToList();
            SyncYoneticiler(existingEntity, managerIds);
        }

        if (receptionistIds is not null)
        {
            var previousReceptionistUserIds = existingEntity.Resepsiyonistler
                .Select(x => x.UserId)
                .Distinct()
                .ToList();

            SyncResepsiyonistler(existingEntity, receptionistIds);
            _tesisRepository.Update(existingEntity);
            await _tesisRepository.SaveChangesAsync();
            if (managerIds is not null)
            {
                await ReconcileOwnerRecordsAfterScopedUserSyncAsync(existingEntity.Id, previousManagerUserIds, managerIds);
            }

            await ReconcileOwnerRecordsAfterScopedUserSyncAsync(existingEntity.Id, previousReceptionistUserIds, receptionistIds);
            return Mapper.Map<TesisDto>(existingEntity);
        }

        _tesisRepository.Update(existingEntity);
        await _tesisRepository.SaveChangesAsync();
        if (managerIds is not null)
        {
            await ReconcileOwnerRecordsAfterScopedUserSyncAsync(existingEntity.Id, previousManagerUserIds, managerIds);
        }

        return Mapper.Map<TesisDto>(existingEntity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanAccessTesisAsync(id);
        await base.DeleteAsync(id);
    }

    public override async Task<TesisDto?> GetByIdAsync(int id, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<TesisDto>> GetAllAsync(Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<TesisDto>> WhereAsync(Expression<Func<Tesis, bool>> predicate, Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<TesisDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<Tesis, bool>>? predicate = null,
        Func<IQueryable<Tesis>, IQueryable<Tesis>>? include = null,
        Func<IQueryable<Tesis>, IOrderedQueryable<Tesis>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureIlRulesAsync(int ilId)
    {
        var il = await _ilRepository.GetByIdAsync(ilId);
        if (il is null)
        {
            throw new BaseException("Secilen il bulunamadi.", 400);
        }

        if (!il.AktifMi)
        {
            throw new BaseException("Pasif il altinda tesis olusturulamaz veya guncellenemez.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.IlIds.Contains(ilId))
        {
            throw new BaseException("Bu il altinda tesis yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(TesisDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _tesisRepository.AnyAsync(x =>
            x.AktifMi &&
            x.IlId == dto.IlId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni il altinda ayni isimde aktif tesis zaten mevcut.", 400);
        }
    }

    private static void Normalize(TesisDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Tesis adi zorunludur.", 400);
        }

        if (dto.IlId <= 0)
        {
            throw new BaseException("Il secimi zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Telefon))
        {
            throw new BaseException("Telefon zorunludur.", 400);
        }

        if (string.IsNullOrWhiteSpace(dto.Adres))
        {
            throw new BaseException("Adres zorunludur.", 400);
        }

        dto.Ad = dto.Ad.Trim();
        dto.Telefon = dto.Telefon.Trim();
        dto.Adres = dto.Adres.Trim();
        dto.Eposta = string.IsNullOrWhiteSpace(dto.Eposta) ? null : dto.Eposta.Trim();
    }

    private async Task EnsureCanAccessTesisAsync(int tesisId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis kaydi icin yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<Tesis>, IQueryable<Tesis>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Tesis>, IQueryable<Tesis>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            result = result
                .Include(x => x.Yoneticiler)
                .Include(x => x.Resepsiyonistler);

            if (scope.IsScoped)
            {
                result = result.Where(x => scope.TesisIds.Contains(x.Id));
            }

            return result;
        };
    }

    private async Task<List<Guid>?> NormalizeAndValidateManagerIdsAsync(
        ICollection<Guid>? managerUserIds,
        bool preserveWhenNull)
    {
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
            .ToListAsync();

        var missingUserIds = normalizedManagerIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
        {
            throw new BaseException("Secilen yoneticilerden en az biri bulunamadi.", 400);
        }

        return normalizedManagerIds;
    }

    private async Task<List<Guid>?> NormalizeAndValidateReceptionistIdsAsync(
        ICollection<Guid>? receptionistUserIds,
        bool preserveWhenNull)
    {
        if (receptionistUserIds is null)
        {
            return preserveWhenNull ? null : [];
        }

        var normalizedReceptionistIds = receptionistUserIds
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (normalizedReceptionistIds.Count == 0)
        {
            return [];
        }

        var existingUserIds = await _userRepository
            .Where(x => normalizedReceptionistIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync();

        var missingUserIds = normalizedReceptionistIds.Except(existingUserIds).ToList();
        if (missingUserIds.Count > 0)
        {
            throw new BaseException("Secilen resepsiyonistlerden en az biri bulunamadi.", 400);
        }

        var receptionistGroupId = await _identityDbContext.UserGroups
            .Where(x => x.UserGroupRoles.Any(ugr =>
                ugr.Role.Domain == nameof(StructurePermissions.KullaniciGrupTipi)
                && ugr.Role.Name == nameof(StructurePermissions.KullaniciGrupTipi.Resepsiyonist)))
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (receptionistGroupId == Guid.Empty)
        {
            throw new BaseException("Resepsiyonist grubu bulunamadi.", 400);
        }

        var receptionistMembershipUserIds = await _identityDbContext.UserUserGroups
            .Where(x => x.UserGroupId == receptionistGroupId && normalizedReceptionistIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();

        var invalidGroupUserIds = normalizedReceptionistIds.Except(receptionistMembershipUserIds).ToList();
        if (invalidGroupUserIds.Count > 0)
        {
            throw new BaseException("Secilen kullanicilar resepsiyonist grubunda olmalidir.", 400);
        }

        return normalizedReceptionistIds;
    }

    private void SyncYoneticiler(Tesis entity, IReadOnlyCollection<Guid> managerUserIds)
    {
        entity.Yoneticiler ??= [];

        var byUserId = entity.Yoneticiler.ToDictionary(x => x.UserId);
        var desiredUserIds = managerUserIds.ToHashSet();

        var toDelete = entity.Yoneticiler
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _tesisYoneticiRepository.DeleteRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Yoneticiler.Add(new TesisYonetici
            {
                UserId = desiredUserId
            });
        }
    }

    private void SyncResepsiyonistler(Tesis entity, IReadOnlyCollection<Guid> receptionistUserIds)
    {
        entity.Resepsiyonistler ??= [];

        var byUserId = entity.Resepsiyonistler.ToDictionary(x => x.UserId);
        var desiredUserIds = receptionistUserIds.ToHashSet();

        var toDelete = entity.Resepsiyonistler
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _tesisResepsiyonistRepository.DeleteRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Resepsiyonistler.Add(new TesisResepsiyonist
            {
                UserId = desiredUserId
            });
        }
    }

    private async Task SyncUserToSingleTesisAsync(Guid userId, int tesisId)
    {
        var existingAssignments = await _tesisResepsiyonistRepository
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var assignmentsToDelete = existingAssignments
            .Where(x => x.TesisId != tesisId)
            .ToList();

        if (assignmentsToDelete.Count > 0)
        {
            _tesisResepsiyonistRepository.DeleteRange(assignmentsToDelete);
        }

        var hasSelectedTesisAssignment = existingAssignments.Any(x => x.TesisId == tesisId);
        if (!hasSelectedTesisAssignment)
        {
            await _tesisResepsiyonistRepository.AddAsync(new TesisResepsiyonist
            {
                TesisId = tesisId,
                UserId = userId
            });
        }

        await _tesisResepsiyonistRepository.SaveChangesAsync();
    }

    private async Task EnsureOwnerRecordExistsAsync(Guid userId, int tesisId)
    {
        var existingOwner = await _stysDbContext.KullaniciTesisSahiplikleri
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (existingOwner is not null)
        {
            if (!existingOwner.TesisId.HasValue)
            {
                existingOwner.TesisId = tesisId;
                _stysDbContext.KullaniciTesisSahiplikleri.Update(existingOwner);
                await _stysDbContext.SaveChangesAsync();
            }
            return;
        }

        await _stysDbContext.KullaniciTesisSahiplikleri.AddAsync(new()
        {
            UserId = userId,
            TesisId = tesisId
        });
        await _stysDbContext.SaveChangesAsync();
    }

    private async Task EnsureOwnerRecordsForAssignedUsersAsync(int tesisId, IReadOnlyCollection<Guid> receptionistUserIds)
    {
        if (receptionistUserIds.Count == 0)
        {
            return;
        }

        var ownerUserIds = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => receptionistUserIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync();

        var missingOwnerUserIds = receptionistUserIds
            .Where(x => !ownerUserIds.Contains(x))
            .ToList();

        if (missingOwnerUserIds.Count == 0)
        {
            return;
        }

        foreach (var userId in missingOwnerUserIds)
        {
            await _stysDbContext.KullaniciTesisSahiplikleri.AddAsync(new()
            {
                UserId = userId,
                TesisId = null
            });
        }

        await _stysDbContext.SaveChangesAsync();
    }

    private async Task ReconcileOwnerRecordsAfterScopedUserSyncAsync(
        int tesisId,
        IReadOnlyCollection<Guid> previousUserIds,
        IReadOnlyCollection<Guid> desiredUserIds)
    {
        await EnsureOwnerRecordsForAssignedUsersAsync(tesisId, desiredUserIds);

        var removedUserIds = previousUserIds
            .Except(desiredUserIds)
            .Distinct()
            .ToList();

        if (removedUserIds.Count == 0)
        {
            return;
        }

        var ownerRows = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => removedUserIds.Contains(x.UserId) && x.TesisId == tesisId)
            .ToListAsync();

        if (ownerRows.Count == 0)
        {
            return;
        }

        foreach (var ownerRow in ownerRows)
        {
            ownerRow.TesisId = null;
            _stysDbContext.KullaniciTesisSahiplikleri.Update(ownerRow);
        }

        await _stysDbContext.SaveChangesAsync();
    }
}
