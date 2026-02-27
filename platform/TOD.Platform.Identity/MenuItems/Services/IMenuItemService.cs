using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.MenuItems.Services;

public interface IMenuItemService : IBaseService<MenuItemDto, MenuItem>
{
    Task<IEnumerable<MenuItemDto>> GetMenuTreeAsync();
}
