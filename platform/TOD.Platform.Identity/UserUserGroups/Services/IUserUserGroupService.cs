using TOD.Platform.Identity.UserUserGroups.DTO;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.UserUserGroups.Services;

public interface IUserUserGroupService : IBaseService<UserUserGroupDto, UserUserGroup>
{
}
