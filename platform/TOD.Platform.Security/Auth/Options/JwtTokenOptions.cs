namespace TOD.Platform.Security.Auth.Options;

public class JwtTokenOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public int AccessTokenExpirationHours { get; set; } = 8;
}
