using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.MenuItemRoles.DTO;

public class MenuItemRoleDto : BaseRdbmsDto<Guid>
{
    public MenuItemDto? MenuItem { get; set; }

    public RoleDto? Role { get; set; }
}
