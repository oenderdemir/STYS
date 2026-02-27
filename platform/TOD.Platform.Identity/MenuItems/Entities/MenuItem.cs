using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace TOD.Platform.Identity.MenuItems.Entities;

public class MenuItem : BaseEntity<Guid>
{
    public string? Label { get; set; }

    public string? Icon { get; set; }

    public string? Route { get; set; }

    public string? QueryParams { get; set; }

    public Guid? ParentId { get; set; }

    public MenuItem? Parent { get; set; }

    public int MenuOrder { get; set; }

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();

    public ICollection<MenuItemRole> MenuItemRoles { get; set; } = new List<MenuItemRole>();
}
