using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.UserGroups.Entities;

public class UserGroup : BaseEntity<Guid>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DefaultRoute { get; set; }

    public ICollection<UserGroupRole> UserGroupRoles { get; set; } = new List<UserGroupRole>();

    public ICollection<UserUserGroup> UserUserGroups { get; set; } = new List<UserUserGroup>();
}
