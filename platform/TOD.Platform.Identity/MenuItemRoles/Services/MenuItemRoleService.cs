using AutoMapper;
using TOD.Platform.Identity.MenuItemRoles.DTO;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItemRoles.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.MenuItemRoles.Services;

public class MenuItemRoleService : BaseRdbmsService<MenuItemRoleDto, MenuItemRole>, IMenuItemRoleService
{
    public MenuItemRoleService(IMenuItemRoleRepository menuItemRoleRepository, IMapper mapper)
        : base(menuItemRoleRepository, mapper)
    {
    }
}
