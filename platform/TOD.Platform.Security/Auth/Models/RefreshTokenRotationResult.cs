namespace TOD.Platform.Security.Auth.Models;

public class RefreshTokenRotationResult<TKey> where TKey : struct
{
    public RefreshTokenRotationStatus Status { get; set; } = RefreshTokenRotationStatus.Invalid;

    public TKey UserId { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
