using Microsoft.AspNetCore.Mvc;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.AspNetCore.Controllers;
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
    [Permission("UserManagement.View")]
    public Task<IEnumerable<UserUserGroupDto>> GetAll()
    {
        return _userUserGroupService.GetAllAsync();
    }
}
