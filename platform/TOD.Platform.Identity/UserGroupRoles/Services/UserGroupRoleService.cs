using AutoMapper;
using TOD.Platform.Identity.Security.Services;
using TOD.Platform.Identity.UserGroupRoles.DTO;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroupRoles.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserGroupRoles.Services;

public class UserGroupRoleService : BaseRdbmsService<UserGroupRoleDto, UserGroupRole>, IUserGroupRoleService
{
    private readonly ITokenInvalidationService _tokenInvalidationService;

    public UserGroupRoleService(
        IUserGroupRoleRepository userGroupRoleRepository,
        ITokenInvalidationService tokenInvalidationService,
        IMapper mapper)
        : base(userGroupRoleRepository, mapper)
    {
        _tokenInvalidationService = tokenInvalidationService;
    }

    public override async Task<UserGroupRoleDto> AddAsync(UserGroupRoleDto dto)
    {
        var created = await base.AddAsync(dto);
        var groupId = created.UserGroup?.Id ?? dto.UserGroup?.Id;

        if (groupId.HasValue)
        {
            await _tokenInvalidationService.InvalidateUsersByGroupIdsAsync([groupId.Value], "Group permission added", CancellationToken.None);
        }

        return created;
    }

    public override async Task<UserGroupRoleDto> UpdateAsync(UserGroupRoleDto dto)
    {
        var affectedGroupIds = new HashSet<Guid>();
        if (dto.Id.HasValue)
        {
            var existingEntity = await Repository.GetByIdAsync(dto.Id.Value);
            if (existingEntity is not null)
            {
                affectedGroupIds.Add(existingEntity.UserGroupId);
            }
        }

        if (dto.UserGroup?.Id.HasValue == true)
        {
            affectedGroupIds.Add(dto.UserGroup.Id.Value);
        }

        var updated = await base.UpdateAsync(dto);
        if (updated.UserGroup?.Id.HasValue == true)
        {
            affectedGroupIds.Add(updated.UserGroup.Id.Value);
        }

        await _tokenInvalidationService.InvalidateUsersByGroupIdsAsync(affectedGroupIds, "Group permission updated", CancellationToken.None);
        return updated;
    }

    public override async Task DeleteAsync(Guid id)
    {
        var existingEntity = await Repository.GetByIdAsync(id);
        await base.DeleteAsync(id);

        if (existingEntity is not null)
        {
            await _tokenInvalidationService.InvalidateUsersByGroupIdsAsync([existingEntity.UserGroupId], "Group permission removed", CancellationToken.None);
        }
    }
}
