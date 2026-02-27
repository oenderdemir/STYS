using AutoMapper;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Identity.Roles.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.Roles.Services;

public class RoleService : BaseRdbmsService<RoleDto, Role>, IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository, IMapper mapper)
        : base(roleRepository, mapper)
    {
        _roleRepository = roleRepository;
    }

    public async Task<RoleDto?> GetByNameAsync(string name)
    {
        var entity = await _roleRepository.GetByNameAsync(name);
        return Mapper.Map<RoleDto?>(entity);
    }

    public async Task<IEnumerable<RoleDto>> GetViewRolesAsync()
    {
        var entities = await _roleRepository.GetViewRolesAsync();
        return Mapper.Map<IEnumerable<RoleDto>>(entities);
    }
}
