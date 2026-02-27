using TOD.Platform.Identity.MenuItemRoles.DTO;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.MenuItemRoles.Services;

public interface IMenuItemRoleService : IBaseService<MenuItemRoleDto, MenuItemRole>
{
}
