using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace TOD.Platform.Identity.Roles.Repositories;

public class RoleRepository : BaseRdbmsRepository<Role>, IRoleRepository
{
    public RoleRepository(TodIdentityDbContext context, IMapper mapper)
        : base(context, mapper)
    {
    }

    public Task<Role?> GetByNameAsync(string name)
    {
        return FirstOrDefaultAsync(x => x.Name == name);
    }

    public async Task<IEnumerable<Role>> GetViewRolesAsync()
    {
        var menuRoles = await Where(x => x.Name == "Menu").ToListAsync();
        if (menuRoles.Count > 0)
        {
            return menuRoles;
        }

        return await Where(x => x.Name == "View").ToListAsync();
    }
}
