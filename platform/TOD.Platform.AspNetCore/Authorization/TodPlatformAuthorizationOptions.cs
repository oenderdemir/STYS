namespace TOD.Platform.AspNetCore.Authorization;

public sealed class TodPlatformAuthorizationOptions
{
    public string PermissionClaimType { get; set; } = TodPlatformAuthorizationConstants.PermissionClaimType;
    public string AdminPermission { get; set; } = TodPlatformAuthorizationConstants.AdminPermission;
    public string UiUserPermission { get; set; } = TodPlatformAuthorizationConstants.UiUserPermission;
    public string ServiceUserPermission { get; set; } = TodPlatformAuthorizationConstants.ServiceUserPermission;
    public string UiScheme { get; set; } = TodPlatformAuthorizationConstants.UiScheme;
    public string ServiceScheme { get; set; } = TodPlatformAuthorizationConstants.ServiceScheme;
}
