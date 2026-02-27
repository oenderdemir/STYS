using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TOD.Platform.Security.Auth.Services;

public interface ICurrentUserAccessor
{
    string? GetCurrentUserName();
}

public class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return null;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("userName")
            ?? user.Identity?.Name;
    }
}
