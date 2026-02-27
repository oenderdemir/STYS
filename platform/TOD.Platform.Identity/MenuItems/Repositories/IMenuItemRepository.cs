using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.MenuItems.Repositories;

public interface IMenuItemRepository : IBaseRepository<MenuItem>
{
    void RemoveMenuItemRolesRange(IEnumerable<MenuItemRole> menuItemRoles);
}
