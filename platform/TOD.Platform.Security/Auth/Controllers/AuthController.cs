using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TOD.Platform.Security.Auth.DTO;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.Security.Auth.Controllers;

[Route("auth/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "stys.refresh_token";
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
            DeleteRefreshTokenCookie();
            const string message = "Kullanıcı adı veya parola yanlış.";
            const string message2 = "Login işlemi başarısız.";
            return Unauthorized(new
            {
                success = false,
                message2,
                data = (object?)null,
                errors = new[]
                {
                    new
                    {
                        code = "INVALID_CREDENTIALS",
                        field = (string?)null,
                        detail = message
                    }
                },
                traceId = HttpContext.TraceIdentifier
            });
        }

        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpireDate);
        response.RefreshToken = string.Empty;
        return response;
    }

    [HttpPost("changePassword")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto model)
    {
        return await _authenticationService.ChangePassword(model);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(new
            {
                success = false,
                message = "Refresh token is missing.",
                data = (object?)null,
                traceId = HttpContext.TraceIdentifier
            });
        }

        var response = await _authenticationService.RefreshAsync(new RefreshTokenRequestDto
        {
            RefreshToken = refreshToken
        });

        if (!response.AuthenticateResult)
        {
            DeleteRefreshTokenCookie();
            return Unauthorized(new
            {
                success = false,
                message = "Refresh token is invalid or expired.",
                data = (object?)null,
                traceId = HttpContext.TraceIdentifier
            });
        }

        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpireDate);
        response.RefreshToken = string.Empty;
        return response;
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Logout(CancellationToken cancellationToken)
    {
        LoginResponseDto response;
        if (User.Identity?.IsAuthenticated == true)
        {
            response = await _authenticationService.LogoutAsync(cancellationToken);
        }
        else
        {
            response = new LoginResponseDto
            {
                AuthenticateResult = false,
                AuthToken = string.Empty,
                AccessTokenExpireDate = DateTime.UtcNow,
                RefreshToken = string.Empty,
                RefreshTokenExpireDate = null,
                UserStatus = null
            };
        }

        DeleteRefreshTokenCookie();
        response.RefreshToken = string.Empty;
        response.RefreshTokenExpireDate = null;
        return Ok(response);
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime? expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || !expiresAtUtc.HasValue)
        {
            return;
        }

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildCookieOptions(expiresAtUtc.Value));
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, BuildCookieOptions(DateTime.UtcNow));
    }

    private CookieOptions BuildCookieOptions(DateTime expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAtUtc
        };
    }
}
