using AutoMapper;
using TOD.Platform.Identity.Security.Services;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Identity.Roles.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.Roles.Services;

public class RoleService : BaseRdbmsService<RoleDto, Role>, IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly ITokenInvalidationService _tokenInvalidationService;

    public RoleService(
        IRoleRepository roleRepository,
        ITokenInvalidationService tokenInvalidationService,
        IMapper mapper)
        : base(roleRepository, mapper)
    {
        _roleRepository = roleRepository;
        _tokenInvalidationService = tokenInvalidationService;
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

    public override async Task<RoleDto> UpdateAsync(RoleDto dto)
    {
        var roleId = dto.Id;
        var updated = await base.UpdateAsync(dto);

        if (roleId.HasValue)
        {
            await _tokenInvalidationService.InvalidateUsersByRoleIdsAsync([roleId.Value], "Role updated", CancellationToken.None);
        }

        return updated;
    }

    public override async Task DeleteAsync(Guid id)
    {
        await base.DeleteAsync(id);
        await _tokenInvalidationService.InvalidateUsersByRoleIdsAsync([id], "Role removed", CancellationToken.None);
    }
}
