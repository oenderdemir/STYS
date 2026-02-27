using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.Users.Repositories;

public interface IUserRepository : IBaseRdbmsRepository<User>
{
    Task<User?> GetByUserNameAsync(string userName);
}
