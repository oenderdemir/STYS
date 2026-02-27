using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.UserUserGroups.DTO;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Identity.UserUserGroups.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.UserUserGroups.Services;

public class UserUserGroupService : BaseRdbmsService<UserUserGroupDto, UserUserGroup>, IUserUserGroupService
{
    public UserUserGroupService(IUserUserGroupRepository userUserGroupRepository, IMapper mapper)
        : base(userUserGroupRepository, mapper)
    {
    }

    public override Task<IEnumerable<UserUserGroupDto>> GetAllAsync(Func<IQueryable<UserUserGroup>, IQueryable<UserUserGroup>>? include = null)
    {
        return base.GetAllAsync(q => q.Include(x => x.User).Include(x => x.UserGroup));
    }
}
