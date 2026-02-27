using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Persistence.RDBMS.Dto;

namespace TOD.Platform.Identity.UserGroups.DTO;

public class UserGroupDto : BaseRdbmsDto<Guid>
{
    public string Name { get; set; } = string.Empty;

    public List<RoleDto>? Roles { get; set; } = new();
}
