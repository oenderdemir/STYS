namespace TOD.Platform.Security.Auth.DTO;

public class GenerateTokenResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime TokenExpireDate { get; set; }

    public string Jti { get; set; } = string.Empty;
}
