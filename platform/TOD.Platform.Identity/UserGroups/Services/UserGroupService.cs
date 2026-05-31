using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Roles.Repositories;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Identity.UserGroups.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserGroups.Services;

public class UserGroupService : BaseRdbmsService<UserGroupDto, UserGroup>, IUserGroupService
{
    private readonly IRoleRepository _roleRepository;

    public UserGroupService(IUserGroupRepository userGroupRepository, IRoleRepository roleRepository, IMapper mapper)
        : base(userGroupRepository, mapper)
    {
        _roleRepository = roleRepository;
    }

    public override async Task<UserGroupDto> AddAsync(UserGroupDto dto)
    {
        NormalizeAndValidateDefaultRoute(dto);

        var userGroup = Mapper.Map<UserGroup>(dto);
        userGroup.UserGroupRoles = new List<UserGroupRole>();

        foreach (var roleId in dto.Roles?.Select(x => x.Id).Where(x => x.HasValue).Select(x => x!.Value).Distinct() ?? Enumerable.Empty<Guid>())
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
            {
                continue;
            }

            userGroup.UserGroupRoles.Add(new UserGroupRole
            {
                UserGroup = userGroup,
                Role = role
            });
        }

        await Repository.AddAsync(userGroup);
        await Repository.SaveChangesAsync();

        return Mapper.Map<UserGroupDto>(userGroup);
    }

    public override async Task<UserGroupDto> UpdateAsync(UserGroupDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id cannot be empty.");
        }

        NormalizeAndValidateDefaultRoute(dto);

        var userGroup = await Repository.GetByIdAsync(dto.Id.Value, q => q.IgnoreQueryFilters().Include(x => x.UserGroupRoles));
        if (userGroup is null)
        {
            throw new InvalidOperationException("User group was not found.");
        }

        userGroup.Name = dto.Name;
        userGroup.DefaultRoute = dto.DefaultRoute;

        var desiredRoleIds = dto.Roles?
            .Select(x => x.Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToHashSet() ?? new HashSet<Guid>();

        var existingRoleIds = userGroup.UserGroupRoles.Select(x => x.RoleId).ToHashSet();

        foreach (var userGroupRole in userGroup.UserGroupRoles)
        {
            userGroupRole.IsDeleted = !desiredRoleIds.Contains(userGroupRole.RoleId);
        }

        foreach (var roleId in desiredRoleIds.Except(existingRoleIds))
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
            {
                continue;
            }

            userGroup.UserGroupRoles.Add(new UserGroupRole
            {
                UserGroupId = userGroup.Id,
                RoleId = roleId,
                UserGroup = userGroup,
                Role = role,
                IsDeleted = false
            });
        }

        Repository.Update(userGroup);
        await Repository.SaveChangesAsync();

        return dto;
    }

    private static void NormalizeAndValidateDefaultRoute(UserGroupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.DefaultRoute))
        {
            dto.DefaultRoute = null;
            return;
        }

        var normalizedRoute = dto.DefaultRoute.Trim();
        if (normalizedRoute.Length > 500)
        {
            throw new InvalidOperationException("Varsayilan sayfa en fazla 500 karakter olabilir.");
        }

        if (!normalizedRoute.StartsWith('/'))
        {
            throw new InvalidOperationException("Varsayilan sayfa '/' ile baslamalidir.");
        }

        dto.DefaultRoute = normalizedRoute;
    }
}
