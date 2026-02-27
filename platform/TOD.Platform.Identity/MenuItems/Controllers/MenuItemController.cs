using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
using TOD.Platform.Identity;
using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.MenuItems.Services;

namespace TOD.Platform.Identity.MenuItems.Controllers;

public class MenuItemController : UIController
{
    private readonly IMenuItemService _menuItemService;

    public MenuItemController(IMenuItemService menuItemService)
    {
        _menuItemService = menuItemService;
    }

    [HttpGet]
    [Permission(IdentityPermissions.MenuManagement.View)]
    public Task<IEnumerable<MenuItemDto>> GetAll()
    {
        return _menuItemService.GetAllAsync(q => q
            .Include(x => x.MenuItemRoles.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Role));
    }

    [HttpGet("{id:guid}")]
    [Permission(IdentityPermissions.MenuManagement.View)]
    public Task<MenuItemDto?> GetById(Guid id)
    {
        return _menuItemService.GetByIdAsync(id, q => q.Include(x => x.MenuItemRoles).ThenInclude(x => x.Role));
    }

    [HttpGet("tree")]

    public Task<IEnumerable<MenuItemDto>> GetTree()
    {
        return _menuItemService.GetMenuTreeAsync();
    }

    [HttpPost]
    [Permission(IdentityPermissions.MenuManagement.Manage)]
    public async Task<ActionResult<MenuItemDto>> Create([FromBody] MenuItemDto dto)
    {
        var created = await _menuItemService.AddAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Permission(IdentityPermissions.MenuManagement.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] MenuItemDto dto)
    {
        dto.Id = id;
        await _menuItemService.UpdateAsync(dto);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [Permission(IdentityPermissions.MenuManagement.Manage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _menuItemService.DeleteAsync(id);
        return Ok();
    }
}
