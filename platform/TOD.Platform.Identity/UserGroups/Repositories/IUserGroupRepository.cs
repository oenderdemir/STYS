using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.UserGroups.Repositories;

public interface IUserGroupRepository : IBaseRepository<UserGroup>
{
    void RemoveUserGroupRolesRange(IEnumerable<UserGroupRole> userGroupRoles);
}
