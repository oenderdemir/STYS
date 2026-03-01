using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TOD.Platform.AspNetCore.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _permissions;
    private const string ViewSuffix = ".View";
    private const string ManageSuffix = ".Manage";

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
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasRequiredPermission = _permissions.Any(requiredPermission =>
            GetAcceptedPermissions(requiredPermission).Any(userPermissions.Contains));

        if (!hasRequiredPermission)
        {
            context.Result = new ForbidResult();
        }
    }

    private static IEnumerable<string> GetAcceptedPermissions(string requiredPermission)
    {
        yield return requiredPermission;

        if (requiredPermission.EndsWith(ViewSuffix, StringComparison.OrdinalIgnoreCase))
        {
            yield return requiredPermission[..^ViewSuffix.Length] + ManageSuffix;
        }
    }
}
