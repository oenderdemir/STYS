using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Persistence.RDBMS.Entities;

namespace TOD.Platform.Identity.MenuItemRoles.Entities;

public class MenuItemRole : BaseEntity<Guid>
{
    public Guid MenuItemId { get; set; }

    public MenuItem MenuItem { get; set; } = default!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = default!;
}
