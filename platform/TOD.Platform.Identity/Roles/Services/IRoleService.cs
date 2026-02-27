using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Identity.Roles.Services;

public interface IRoleService : IBaseService<RoleDto, Role>
{
    Task<RoleDto?> GetByNameAsync(string name);

    Task<IEnumerable<RoleDto>> GetViewRolesAsync();
}
