using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Services;

namespace TOD.Platform.Identity.Users.Controllers;

public class UserController : UIController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Permission(IdentityPermissions.UserManagement.View)]
    public Task<IEnumerable<UserDto>> GetAll()
    {
        return _userService.GetAllAsync(q => q
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role));
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.View)]
    public Task<UserDto?> GetById(Guid id)
    {
        return _userService.GetByIdAsync(id, q => q
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role));
    }

    [HttpPost]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<ActionResult<UserDto>> Create([FromBody] UserDto dto)
    {
        var created = await _userService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserDto dto)
    {
        dto.Id = id;
        await _userService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.UserManagement.Manage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userService.DeleteAsync(id);
        return Ok();
    }
}
