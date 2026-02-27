using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.UserGroups.Repositories;

public class UserGroupRepository : BaseRepository<UserGroup>, IUserGroupRepository
{
    private readonly TodIdentityDbContext _context;

    public UserGroupRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
        _context = context;
    }

    public void RemoveUserGroupRolesRange(IEnumerable<UserGroupRole> userGroupRoles)
    {
        _context.UserGroupRoles.RemoveRange(userGroupRoles);
    }
}
