namespace TOD.Platform.Security.Auth.Options;

public class JwtTokenOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public int AccessTokenExpirationMinutes { get; set; } = 30;

    public int RefreshTokenExpirationDays { get; set; } = 7;

    public int RefreshTokenRetentionDays { get; set; } = 30;

    public int RefreshTokenCleanupIntervalHours { get; set; } = 24;

    public int RefreshTokenCleanupStartupDelayMinutes { get; set; } = 2;
}
