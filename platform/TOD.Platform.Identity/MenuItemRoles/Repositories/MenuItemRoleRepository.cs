using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.MenuItemRoles.Repositories;

public class MenuItemRoleRepository : BaseRepository<MenuItemRole>, IMenuItemRoleRepository
{
    public MenuItemRoleRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}
