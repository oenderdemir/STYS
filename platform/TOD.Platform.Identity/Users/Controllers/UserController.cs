using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
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
    [Permission("UserManagement.View")]
    public Task<IEnumerable<UserDto>> GetAll()
    {
        return _userService.GetAllAsync(q => q
            .Include(x => x.UserUserGroups.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role));
    }
}
