namespace TOD.Platform.Security.Auth.Models;

public enum RefreshTokenRotationStatus
{
    Invalid = 0,
    Rotated = 1,
    ExpiredOrRevoked = 2,
    ReuseDetected = 3
}
