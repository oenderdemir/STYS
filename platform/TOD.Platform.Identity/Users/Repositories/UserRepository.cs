using AutoMapper;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.Users.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }

    public Task<User?> GetByUserNameAsync(string userName)
    {
        return FirstOrDefaultAsync(x => x.UserName == userName);
    }
}
