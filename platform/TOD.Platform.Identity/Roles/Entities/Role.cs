using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.Roles.Entities;

public class Role : BaseEntity<Guid>
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Domain { get; set; } = string.Empty;

    public ICollection<UserGroupRole> UserGroupRoles { get; set; } = new List<UserGroupRole>();

    public ICollection<MenuItemRole> MenuItemRoles { get; set; } = new List<MenuItemRole>();
}
