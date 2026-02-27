using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.UserUserGroups.Entities;

public class UserUserGroup : BaseEntity<Guid>
{
    public Guid UserId { get; set; }

    public User User { get; set; } = default!;

    public Guid UserGroupId { get; set; }

    public UserGroup UserGroup { get; set; } = default!;
}
