using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Persistence.RDBMS.Repositories;

namespace TOD.Platform.Identity.Roles.Repositories;

public interface IRoleRepository : IBaseRepository<Role>
{
    Task<Role?> GetByNameAsync(string name);

    Task<IEnumerable<Role>> GetViewRolesAsync();
}
