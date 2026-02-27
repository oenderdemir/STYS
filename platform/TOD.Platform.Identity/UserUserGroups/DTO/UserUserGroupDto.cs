using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace TOD.Platform.Identity.UserUserGroups.DTO;

public class UserUserGroupDto : BaseRdbmsDto<Guid>
{
    public UserDto? User { get; set; }

    public UserGroupDto? UserGroup { get; set; }
}
