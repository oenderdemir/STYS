using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Entities;

namespace TOD.Platform.Identity.UserGroupRoles.Entities;

public class UserGroupRole : BaseEntity<Guid>
{
    public Guid UserGroupId { get; set; }

    public UserGroup UserGroup { get; set; } = default!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = default!;
}
