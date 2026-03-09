namespace TOD.Platform.Security.Auth.DTO;

public class LoginResponseDto
{
    public bool AuthenticateResult { get; set; }

    public string AuthToken { get; set; } = string.Empty;

    public DateTime AccessTokenExpireDate { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime? RefreshTokenExpireDate { get; set; }

    public string? UserStatus { get; set; }
}
