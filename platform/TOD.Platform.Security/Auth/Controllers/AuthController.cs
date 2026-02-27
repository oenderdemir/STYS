using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.Security.Auth.Controllers;

[Route("auth/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto model)
    {
        var response = await _authenticationService.LoginAsync(model);
        if (!response.AuthenticateResult)
        {
            return new UnauthorizedResult();
        }

        return response;
    }

    [HttpPost("changePassword")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto model)
    {
        return await _authenticationService.ChangePassword(model);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> Logout()
    {
        return await _authenticationService.LogoutAsync();
    }
}
