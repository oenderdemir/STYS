using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.MenuItems.Services;

public interface IMenuItemService : IBaseRdbmsService<MenuItemDto, MenuItem>
{
    Task<IEnumerable<MenuItemDto>> GetMenuTreeAsync();
}
