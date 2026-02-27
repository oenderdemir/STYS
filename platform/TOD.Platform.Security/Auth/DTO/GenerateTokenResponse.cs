namespace TOD.Platform.Security.Auth.DTO;

public class GenerateTokenResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTime TokenExpireDate { get; set; }
}
