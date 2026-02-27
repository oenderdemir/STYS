using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.MenuItems.Repositories;

public class MenuItemRepository : BaseRepository<MenuItem>, IMenuItemRepository
{
    private readonly TodIdentityDbContext _context;

    public MenuItemRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
        _context = context;
    }

    public void RemoveMenuItemRolesRange(IEnumerable<MenuItemRole> menuItemRoles)
    {
        _context.MenuItemRoles.RemoveRange(menuItemRoles);
    }
}
