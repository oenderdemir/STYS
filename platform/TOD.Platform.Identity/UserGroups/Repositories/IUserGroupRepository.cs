using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.UserGroups.Repositories;

public interface IUserGroupRepository : IBaseRdbmsRepository<UserGroup>
{
    void RemoveUserGroupRolesRange(IEnumerable<UserGroupRole> userGroupRoles);
}
