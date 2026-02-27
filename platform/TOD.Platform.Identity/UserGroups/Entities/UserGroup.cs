using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Entities;

namespace TOD.Platform.Identity.UserGroups.Entities;

public class UserGroup : BaseEntity<Guid>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public ICollection<UserGroupRole> UserGroupRoles { get; set; } = new List<UserGroupRole>();

    public ICollection<UserUserGroup> UserUserGroups { get; set; } = new List<UserUserGroup>();
}
