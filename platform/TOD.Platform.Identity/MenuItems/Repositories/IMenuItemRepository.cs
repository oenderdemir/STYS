using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.MenuItems.Repositories;

public interface IMenuItemRepository : IBaseRdbmsRepository<MenuItem>
{
    void RemoveMenuItemRolesRange(IEnumerable<MenuItemRole> menuItemRoles);
}
