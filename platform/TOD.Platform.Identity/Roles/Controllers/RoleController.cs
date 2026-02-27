using Microsoft.AspNetCore.Mvc;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
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
    [Permission(IdentityPermissions.RoleManagement.View)]
    public Task<IEnumerable<RoleDto>> GetViewRoles()
    {
        return _roleService.GetViewRolesAsync();
    }

    [HttpGet]
    [Permission(IdentityPermissions.RoleManagement.View)]
    public Task<IEnumerable<RoleDto>> GetAll()
    {
        return _roleService.GetAllAsync();
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.RoleManagement.View)]
    public Task<RoleDto?> GetById(Guid id)
    {
        return _roleService.GetByIdAsync(id);
    }

    [HttpPost]
    [Permission(IdentityPermissions.RoleManagement.Manage)]
    public async Task<ActionResult<RoleDto>> Create([FromBody] RoleDto dto)
    {
        var created = await _roleService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.RoleManagement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RoleDto dto)
    {
        dto.Id = id;
        await _roleService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.RoleManagement.Manage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _roleService.DeleteAsync(id);
        return Ok();
    }
}
