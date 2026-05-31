using System.ComponentModel.DataAnnotations;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.UserGroups.DTO;

public class UserGroupDto : BaseRdbmsDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? DefaultRoute { get; set; }

    public List<RoleDto>? Roles { get; set; } = new();
}
