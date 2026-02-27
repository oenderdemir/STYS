using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserGroups.Services;

public interface IUserGroupService : IBaseRdbmsService<UserGroupDto, UserGroup>
{
}
