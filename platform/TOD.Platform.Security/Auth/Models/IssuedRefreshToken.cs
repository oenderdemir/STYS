namespace TOD.Platform.Security.Auth.Models;

public class IssuedRefreshToken
{
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
