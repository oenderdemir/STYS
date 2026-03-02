using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using STYS.AccessScope;
using STYS.Binalar.Dto;
using STYS.Binalar.Entities;
using STYS.Binalar.Repositories;
using STYS.Infrastructure.EntityFramework;
using STYS.Tesisler.Repositories;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Binalar.Services;

public class BinaService : BaseRdbmsService<BinaDto, Bina, int>, IBinaService
{
    private readonly IBinaRepository _binaRepository;
    private readonly IBinaYoneticiRepository _binaYoneticiRepository;
    private readonly ITesisRepository _tesisRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserAccessScopeService _userAccessScopeService;
    private readonly StysAppDbContext _stysDbContext;

    public BinaService(
        IBinaRepository binaRepository,
        IBinaYoneticiRepository binaYoneticiRepository,
        ITesisRepository tesisRepository,
        IUserRepository userRepository,
        StysAppDbContext stysDbContext,
        IUserAccessScopeService userAccessScopeService,
        IMapper mapper)
        : base(binaRepository, mapper)
    {
        _binaRepository = binaRepository;
        _binaYoneticiRepository = binaYoneticiRepository;
        _tesisRepository = tesisRepository;
        _userRepository = userRepository;
        _stysDbContext = stysDbContext;
        _userAccessScopeService = userAccessScopeService;
    }

    public override async Task<BinaDto> AddAsync(BinaDto dto)
    {
        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueActiveNameAsync(dto, null);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: false);

        var entity = Mapper.Map<Bina>(dto);
        entity.Yoneticiler = managerIds!
            .Select(x => new BinaYonetici
            {
                UserId = x
            })
            .ToList();

        await _binaRepository.AddAsync(entity);
        await _binaRepository.SaveChangesAsync();
        await EnsureOwnerRecordsForAssignedUsersAsync(entity.TesisId, managerIds!);

        return Mapper.Map<BinaDto>(entity);
    }

    public override async Task<BinaDto> UpdateAsync(BinaDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new BaseException("Bina id zorunludur.", 400);
        }

        var existingEntity = await _binaRepository.GetByIdAsync(dto.Id.Value, query => query.Include(x => x.Yoneticiler));
        if (existingEntity is null)
        {
            throw new BaseException("Guncellenecek bina bulunamadi.", 404);
        }

        await EnsureCanAccessBinaAsync(existingEntity.Id);
        Normalize(dto);
        await EnsureTesisRulesAsync(dto.TesisId);
        await EnsureUniqueActiveNameAsync(dto, dto.Id.Value);
        var managerIds = await NormalizeAndValidateManagerIdsAsync(dto.YoneticiUserIds, preserveWhenNull: true);
        List<Guid> previousManagerUserIds = [];

        existingEntity.IsDeleted = false;
        existingEntity.Ad = dto.Ad;
        existingEntity.TesisId = dto.TesisId;
        existingEntity.KatSayisi = dto.KatSayisi;
        existingEntity.AktifMi = dto.AktifMi;

        if (managerIds is not null)
        {
            previousManagerUserIds = existingEntity.Yoneticiler
                .Select(x => x.UserId)
                .Distinct()
                .ToList();
            SyncYoneticiler(existingEntity, managerIds);
        }

        _binaRepository.Update(existingEntity);
        await _binaRepository.SaveChangesAsync();
        if (managerIds is not null)
        {
            await ReconcileOwnerRecordsAfterScopedUserSyncAsync(existingEntity.TesisId, previousManagerUserIds, managerIds);
        }

        return Mapper.Map<BinaDto>(existingEntity);
    }

    public override async Task DeleteAsync(int id)
    {
        await EnsureCanAccessBinaAsync(id);
        await base.DeleteAsync(id);
    }

    public override async Task<BinaDto?> GetByIdAsync(int id, Func<IQueryable<Bina>, IQueryable<Bina>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetByIdAsync(id, includeQuery);
    }

    public override async Task<IEnumerable<BinaDto>> GetAllAsync(Func<IQueryable<Bina>, IQueryable<Bina>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetAllAsync(includeQuery);
    }

    public override async Task<IEnumerable<BinaDto>> WhereAsync(Expression<Func<Bina, bool>> predicate, Func<IQueryable<Bina>, IQueryable<Bina>>? include = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.WhereAsync(predicate, includeQuery);
    }

    public override async Task<PagedResult<BinaDto>> GetPagedAsync(
        PagedRequest request,
        Expression<Func<Bina, bool>>? predicate = null,
        Func<IQueryable<Bina>, IQueryable<Bina>>? include = null,
        Func<IQueryable<Bina>, IOrderedQueryable<Bina>>? orderBy = null)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        var includeQuery = BuildScopedIncludeQuery(scope, include);
        return await base.GetPagedAsync(request, predicate, includeQuery, orderBy);
    }

    private async Task EnsureTesisRulesAsync(int tesisId)
    {
        var tesis = await _tesisRepository.GetByIdAsync(tesisId);
        if (tesis is null)
        {
            throw new BaseException("Secilen tesis bulunamadi.", 400);
        }

        if (!tesis.AktifMi)
        {
            throw new BaseException("Pasif tesis altinda bina olusturulamaz veya guncellenemez.", 400);
        }

        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (scope.IsScoped && !scope.TesisIds.Contains(tesisId))
        {
            throw new BaseException("Bu tesis altinda bina yonetme yetkiniz bulunmuyor.", 403);
        }
    }

    private async Task EnsureUniqueActiveNameAsync(BinaDto dto, int? excludedId)
    {
        if (!dto.AktifMi)
        {
            return;
        }

        var normalizedName = dto.Ad.Trim().ToUpperInvariant();
        var exists = await _binaRepository.AnyAsync(x =>
            x.AktifMi &&
            x.TesisId == dto.TesisId &&
            x.Ad.ToUpper() == normalizedName &&
            (!excludedId.HasValue || x.Id != excludedId.Value));

        if (exists)
        {
            throw new BaseException("Ayni tesis altinda ayni isimde aktif bina zaten mevcut.", 400);
        }
    }

    private static void Normalize(BinaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad))
        {
            throw new BaseException("Bina adi zorunludur.", 400);
        }

        if (dto.TesisId <= 0)
        {
            throw new BaseException("Tesis secimi zorunludur.", 400);
        }

        if (dto.KatSayisi <= 0)
        {
            throw new BaseException("Kat sayisi sifirdan buyuk olmalidir.", 400);
        }

        dto.Ad = dto.Ad.Trim();
    }

    private async Task EnsureCanAccessBinaAsync(int binaId)
    {
        var scope = await _userAccessScopeService.GetCurrentScopeAsync();
        if (!scope.IsScoped)
        {
            return;
        }

        if (!scope.BinaIds.Contains(binaId))
        {
            throw new BaseException("Bu bina kaydi icin yetkiniz bulunmuyor.", 403);
        }
    }

    private static Func<IQueryable<Bina>, IQueryable<Bina>> BuildScopedIncludeQuery(
        DomainAccessScope scope,
        Func<IQueryable<Bina>, IQueryable<Bina>>? include)
    {
        return query =>
        {
            var result = include is null ? query : include(query);
            result = result.Include(x => x.Yoneticiler);

            if (scope.IsScoped)
            {
                result = result.Where(x => scope.BinaIds.Contains(x.Id));
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

    private void SyncYoneticiler(Bina entity, IReadOnlyCollection<Guid> managerUserIds)
    {
        entity.Yoneticiler ??= [];

        var byUserId = entity.Yoneticiler.ToDictionary(x => x.UserId);
        var desiredUserIds = managerUserIds.ToHashSet();

        var toDelete = entity.Yoneticiler
            .Where(x => !desiredUserIds.Contains(x.UserId))
            .ToList();

        if (toDelete.Count > 0)
        {
            _binaYoneticiRepository.DeleteRange(toDelete);
        }

        foreach (var desiredUserId in desiredUserIds)
        {
            if (byUserId.ContainsKey(desiredUserId))
            {
                continue;
            }

            entity.Yoneticiler.Add(new BinaYonetici
            {
                UserId = desiredUserId
            });
        }
    }

    private async Task EnsureOwnerRecordsForAssignedUsersAsync(int tesisId, IReadOnlyCollection<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var ownerUserIds = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync();

        var missingOwnerUserIds = userIds
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
