using TOD.Platform.Identity.UserGroupRoles.DTO;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.UserGroupRoles.Services;

public interface IUserGroupRoleService : IBaseService<UserGroupRoleDto, UserGroupRole>
{
}
