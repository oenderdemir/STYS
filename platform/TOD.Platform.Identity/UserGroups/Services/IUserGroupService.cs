using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.UserGroups.Services;

public interface IUserGroupService : IBaseService<UserGroupDto, UserGroup>
{
}
