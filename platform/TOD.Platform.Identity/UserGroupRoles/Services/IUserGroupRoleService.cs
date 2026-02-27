using TOD.Platform.Identity.UserGroupRoles.DTO;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserGroupRoles.Services;

public interface IUserGroupRoleService : IBaseRdbmsService<UserGroupRoleDto, UserGroupRole>
{
}
