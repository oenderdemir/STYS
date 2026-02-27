using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Persistence.RDBMS.Dto;

namespace TOD.Platform.Identity.UserGroupRoles.DTO;

public class UserGroupRoleDto : BaseRdbmsDto<Guid>
{
    public UserGroupDto? UserGroup { get; set; }

    public RoleDto? Role { get; set; }
}
