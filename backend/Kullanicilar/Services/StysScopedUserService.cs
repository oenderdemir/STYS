using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
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

    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IAccessScopeProvider _accessScopeProvider;

    public StysScopedUserService(
        IUserRepository userRepository,
        IUserGroupRepository userGroupRepository,
        IPasswordHasher passwordHasher,
        TodIdentityDbContext identityDbContext,
        IAccessScopeProvider accessScopeProvider,
        AutoMapper.IMapper mapper)
        : base(userRepository, userGroupRepository, passwordHasher, mapper)
    {
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
            var receptionistGroupId = await GetRequiredGroupIdAsync(ResepsiyonistGroupName);
            dto.UserGroups = [new UserGroupDto { Id = receptionistGroupId }];
        }

        return await base.AddAsync(dto);
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
            await EnsureScopedTesisManagerCanManageUserAsync(dto.Id.Value, actorScope);
            var receptionistGroupId = await GetRequiredGroupIdAsync(ResepsiyonistGroupName);
            dto.UserGroups = [new UserGroupDto { Id = receptionistGroupId }];
        }

        return await base.UpdateAsync(dto);
    }

    public override async Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedTesisManagerCanManageUserAsync(id, actorScope);
        }

        await base.ResetPasswordAsync(id, dto);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync();
        if (actorScope.IsTesisManagerScoped)
        {
            await EnsureScopedTesisManagerCanManageUserAsync(id, actorScope);
        }

        await base.DeleteAsync(id);
    }

    private async Task EnsureScopedTesisManagerCanManageUserAsync(
        Guid targetUserId,
        UserActorScope actorScope,
        CancellationToken cancellationToken = default)
    {
        if (!actorScope.IsTesisManagerScoped)
        {
            return;
        }

        var isResepsiyonist = await _identityDbContext.UserUserGroups
            .AnyAsync(x => x.UserId == targetUserId && x.UserGroup.Name == ResepsiyonistGroupName, cancellationToken);

        if (!isResepsiyonist)
        {
            throw new InvalidOperationException("Tesis yoneticisi yalnizca resepsiyonist kullanicilari yonetebilir.");
        }

        if (actorScope.VisibleUserIds.Contains(targetUserId))
        {
            return;
        }

        throw new InvalidOperationException("Bu kullaniciyi yonetme yetkiniz bulunmuyor.");
    }

    private async Task<Guid> GetRequiredGroupIdAsync(string groupName, CancellationToken cancellationToken = default)
    {
        var groupId = await _identityDbContext.UserGroups
            .Where(x => x.Name == groupName)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (groupId == Guid.Empty)
        {
            throw new InvalidOperationException($"{groupName} bulunamadi.");
        }

        return groupId;
    }

}
