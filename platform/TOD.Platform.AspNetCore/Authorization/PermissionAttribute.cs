using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TOD.Platform.AspNetCore.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _permissions;

    public PermissionAttribute(params string[] permissions)
    {
        _permissions = permissions ?? Array.Empty<string>();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (_permissions.Length == 0)
        {
            return;
        }

        var userPermissions = user.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        var hasRequiredPermission = _permissions.Any(p => userPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
        var isAdmin = userPermissions.Any(p => p.EndsWith(".Admin", StringComparison.OrdinalIgnoreCase));

        if (!hasRequiredPermission && !isAdmin)
        {
            context.Result = new ForbidResult();
        }
    }
}
