using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.UserUserGroups.DTO;
using TOD.Platform.Identity.UserUserGroups.Services;

namespace TOD.Platform.Identity.UserUserGroups.Controllers;

public class UserUserGroupController : UIController
{
    private readonly IUserUserGroupService _userUserGroupService;

    public UserUserGroupController(IUserUserGroupService userUserGroupService)
    {
        _userUserGroupService = userUserGroupService;
    }

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View)]
    public Task<IEnumerable<UserUserGroupDto>> GetAll()
    {
        return _userUserGroupService.GetAllAsync();
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.View)]
    public Task<UserUserGroupDto?> GetById(Guid id)
    {
        return _userUserGroupService.GetByIdAsync(id, q => q.Include(x => x.User).Include(x => x.UserGroup));
    }

    [HttpPost]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserUserGroupDto>> Create([FromBody] UserUserGroupDto dto)
    {
        var created = await _userUserGroupService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserUserGroupDto dto)
    {
        dto.Id = id;
        await _userUserGroupService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userUserGroupService.DeleteAsync(id);
        return Ok();
    }
}
