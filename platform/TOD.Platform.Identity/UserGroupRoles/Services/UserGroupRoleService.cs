using AutoMapper;
using TOD.Platform.Identity.UserGroupRoles.DTO;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroupRoles.Repositories;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.UserGroupRoles.Services;

public class UserGroupRoleService : BaseService<UserGroupRoleDto, UserGroupRole>, IUserGroupRoleService
{
    public UserGroupRoleService(IUserGroupRoleRepository userGroupRoleRepository, IMapper mapper)
        : base(userGroupRoleRepository, mapper)
    {
    }
}
