using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.UserUserGroups.Repositories;

public class UserUserGroupRepository : BaseRepository<UserUserGroup>, IUserUserGroupRepository
{
    public UserUserGroupRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }
}
