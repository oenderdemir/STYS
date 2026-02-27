using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Persistence.RDBMS.Dto;

namespace TOD.Platform.Identity.MenuItems.DTO;

public class MenuItemDto : BaseRdbmsDto<Guid>
{
    public string? Label { get; set; }

    public string? Icon { get; set; }

    public string? Route { get; set; }

    public string? QueryParams { get; set; }

    public Guid? ParentId { get; set; }

    public int MenuOrder { get; set; }

    public List<RoleDto>? Roles { get; set; } = new();

    public List<MenuItemDto>? Items { get; set; }
}
