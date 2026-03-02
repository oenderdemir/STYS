using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Repositories;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Security.Auth.Services;
using BaseUserService = TOD.Platform.Identity.Users.Services.UserService;

namespace STYS.Kullanicilar.Services;

public class StysScopedUserService : BaseUserService
{
    private const string ResepsiyonistGroupName = "ResepsiyonistGrubu";
    private const string TesisYoneticiGroupName = "TesisYoneticiGrubu";
    private const string BinaYoneticiGroupName = "BinaYoneticiGrubu";

    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IAccessScopeProvider _accessScopeProvider;

    public StysScopedUserService(
        IUserRepository userRepository,
        IUserGroupRepository userGroupRepository,
        IPasswordHasher passwordHasher,
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        IAccessScopeProvider accessScopeProvider,
        AutoMapper.IMapper mapper)
        : base(userRepository, userGroupRepository, passwordHasher, mapper)
    {
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _accessScopeProvider = accessScopeProvider;
    }

    public override async Task<IEnumerable<UserDto>> GetAllAsync(Func<IQueryable<User>, IQueryable<User>>? include = null)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();

        var query = include is null
            ? Repository.Where(_ => true)
            : include(Repository.Where(_ => true));

        if (actorScope.IsTesisManagerScoped)
        {
            query = query.Where(x => actorScope.VisibleUserIds.Contains(x.Id));
        }

        var users = await query.ToListAsync();
        return Mapper.Map<IEnumerable<UserDto>>(users);
    }

    public override async Task<UserDto?> GetByIdAsync(Guid id, Func<IQueryable<User>, IQueryable<User>>? include = null)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();

        if (actorScope.IsTesisManagerScoped && !actorScope.VisibleUserIds.Contains(id))
        {
            return null;
        }

        var query = include is null
            ? Repository.Where(x => x.Id == id)
            : include(Repository.Where(x => x.Id == id));

        var entity = await query.FirstOrDefaultAsync();
        return Mapper.Map<UserDto?>(entity);
    }

    public override async Task<UserDto> AddAsync(UserDto dto)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await ValidateScopedManagerGroupSelectionAsync(dto, CancellationToken.None);
        }

        var created = await base.AddAsync(dto);
        if (actorScope.IsTesisManagerScoped && created.Id.HasValue)
        {
            await EnsureOwnerRecordForScopedCreateAsync(created.Id.Value, actorScope, CancellationToken.None);
        }

        return created;
    }

    public override async Task<UserDto> UpdateAsync(UserDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id cannot be empty.");
        }

        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(dto.Id.Value, actorScope);
            await ValidateScopedManagerGroupSelectionAsync(dto, CancellationToken.None);
        }

        return await base.UpdateAsync(dto);
    }

    public override async Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(id, actorScope);
        }

        await base.ResetPasswordAsync(id, dto);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedManagerCanManageUserAsync(id, actorScope);
        }

        await base.DeleteAsync(id);

        var ownerRows = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.UserId == id)
            .ToListAsync();

        if (ownerRows.Count > 0)
        {
            _stysDbContext.KullaniciTesisSahiplikleri.RemoveRange(ownerRows);
            await _stysDbContext.SaveChangesAsync();
        }
    }

    private async Task EnsureScopedManagerCanManageUserAsync(
        Guid targetUserId,
        UserActorScope actorScope,
        CancellationToken cancellationToken = default)
    {
        if (!actorScope.IsTesisManagerScoped)
        {
            return;
        }

        var managedGroupNames = GetScopedManageableGroupNames();
        var isTargetInManageableGroup = await _identityDbContext.UserUserGroups
            .AnyAsync(
                x => x.UserId == targetUserId && managedGroupNames.Contains(x.UserGroup.Name),
                cancellationToken);

        if (!isTargetInManageableGroup)
        {
            throw new InvalidOperationException("Bu kullanicinin grubu scoped yonetim kapsaminda degil.");
        }

        var ownerTesisId = await _stysDbContext.KullaniciTesisSahiplikleri
            .Where(x => x.UserId == targetUserId)
            .Select(x => x.TesisId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!ownerTesisId.HasValue)
        {
            throw new InvalidOperationException("Bu kullanici sahipsiz oldugu icin global kullanici bilgileri tesis yoneticisi tarafindan degistirilemez.");
        }

        if (ownerTesisId.Value <= 0)
        {
            throw new InvalidOperationException("Resepsiyonist icin sahip tesis bilgisi tanimli degil.");
        }

        if (!actorScope.ManagedTesisIds.Contains(ownerTesisId.Value))
        {
            throw new InvalidOperationException("Bu kullanicinin global bilgilerini duzenleme yetkiniz yok.");
        }

        if (actorScope.VisibleUserIds.Contains(targetUserId))
        {
            return;
        }

        throw new InvalidOperationException("Bu kullaniciyi yonetme yetkiniz bulunmuyor.");
    }

    private async Task ValidateScopedManagerGroupSelectionAsync(UserDto dto, CancellationToken cancellationToken)
    {
        var managedGroupIdByName = await GetScopedManageableGroupIdsByNameAsync(cancellationToken);
        var managedGroupIds = managedGroupIdByName.Values.ToHashSet();

        if (managedGroupIds.Count == 0)
        {
            throw new InvalidOperationException("Scoped yonetim icin gerekli kullanici gruplari bulunamadi.");
        }

        var requestedGroupIds = dto.UserGroups
            .Select(x => x.Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        if (requestedGroupIds.Count == 0)
        {
            var defaultGroupId = managedGroupIdByName[ResepsiyonistGroupName];
            dto.UserGroups = [new UserGroupDto { Id = defaultGroupId }];
            return;
        }

        var invalidGroupIds = requestedGroupIds
            .Where(x => !managedGroupIds.Contains(x))
            .ToList();

        if (invalidGroupIds.Count > 0)
        {
            throw new InvalidOperationException("Scoped yonetici yalnizca resepsiyonist, bina yoneticisi veya tesis yoneticisi gruplarinda kullanici yonetebilir.");
        }
    }

    private async Task EnsureOwnerRecordForScopedCreateAsync(
        Guid userId,
        UserActorScope actorScope,
        CancellationToken cancellationToken)
    {
        if (actorScope.ManagedTesisIds.Count == 0)
        {
            return;
        }

        var ownerTesisId = actorScope.ManagedTesisIds.Min();
        var existingOwner = await _stysDbContext.KullaniciTesisSahiplikleri
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (existingOwner is null)
        {
            await _stysDbContext.KullaniciTesisSahiplikleri.AddAsync(new()
            {
                UserId = userId,
                TesisId = ownerTesisId
            }, cancellationToken);
            await _stysDbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!existingOwner.TesisId.HasValue)
        {
            existingOwner.TesisId = ownerTesisId;
            _stysDbContext.KullaniciTesisSahiplikleri.Update(existingOwner);
            await _stysDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, Guid>> GetScopedManageableGroupIdsByNameAsync(CancellationToken cancellationToken)
    {
        var groupNames = GetScopedManageableGroupNames();
        var groups = await _identityDbContext.UserGroups
            .Where(x => groupNames.Contains(x.Name))
            .Select(x => new { x.Name, x.Id })
            .ToListAsync(cancellationToken);

        var groupIdByName = groups.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var groupName in groupNames)
        {
            if (!groupIdByName.ContainsKey(groupName))
            {
                throw new InvalidOperationException($"{groupName} bulunamadi.");
            }
        }

        return groupIdByName;
    }

    private static HashSet<string> GetScopedManageableGroupNames()
    {
        return
        [
            ResepsiyonistGroupName,
            TesisYoneticiGroupName,
            BinaYoneticiGroupName
        ];
    }

}
