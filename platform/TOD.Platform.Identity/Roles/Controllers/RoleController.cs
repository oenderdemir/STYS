using Microsoft.AspNetCore.Mvc;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity.Roles.DTO;
using TOD.Platform.Identity.Roles.Services;

namespace TOD.Platform.Identity.Roles.Controllers;

public class RoleController : UIController
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet("view-roles")]
    [Permission("RoleManagement.View")]
    public Task<IEnumerable<RoleDto>> GetViewRoles()
    {
        return _roleService.GetViewRolesAsync();
    }

    [HttpGet]
    [Permission("RoleManagement.View")]
    public Task<IEnumerable<RoleDto>> GetAll()
    {
        return _roleService.GetAllAsync();
    }
}
