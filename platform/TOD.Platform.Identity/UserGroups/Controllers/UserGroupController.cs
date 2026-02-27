using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity.UserGroups.DTO;
using TOD.Platform.Identity.UserGroups.Services;

namespace TOD.Platform.Identity.UserGroups.Controllers;

public class UserGroupController : UIController
{
    private readonly IUserGroupService _userGroupService;

    public UserGroupController(IUserGroupService userGroupService)
    {
        _userGroupService = userGroupService;
    }

    [HttpGet]
    [Permission("UserGroupManagement.View")]
    public Task<IEnumerable<UserGroupDto>> GetAll()
    {
        return _userGroupService.GetAllAsync(q => q
            .Include(x => x.UserGroupRoles.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Role));
    }

    [HttpGet("{id:guid}")]
    [Permission("UserGroupManagement.View")]
    public Task<UserGroupDto?> GetById(Guid id)
    {
        return _userGroupService.GetByIdAsync(id, q => q.Include(x => x.UserGroupRoles).ThenInclude(x => x.Role));
    }

    [HttpPost]
    [Permission("UserGroupManagement.Manage")]
    public async Task<ActionResult<UserGroupDto>> Create([FromBody] UserGroupDto dto)
    {
        var created = await _userGroupService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission("UserGroupManagement.Manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserGroupDto dto)
    {
        dto.Id = id;
        await _userGroupService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission("UserGroupManagement.Manage")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userGroupService.DeleteAsync(id);
        return Ok();
    }
}
