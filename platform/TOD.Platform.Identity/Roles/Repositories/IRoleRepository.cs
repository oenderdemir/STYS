using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.Roles.Repositories;

public interface IRoleRepository : IBaseRdbmsRepository<Role>
{
    Task<Role?> GetByNameAsync(string name);

    Task<IEnumerable<Role>> GetViewRolesAsync();
}
