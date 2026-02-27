using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.UserGroupRoles.Repositories;

public class UserGroupRoleRepository : BaseRdbmsRepository<UserGroupRole>, IUserGroupRoleRepository
{
    public UserGroupRoleRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}
