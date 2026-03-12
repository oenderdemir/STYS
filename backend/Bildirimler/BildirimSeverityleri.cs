namespace STYS.Bildirimler;

public static class BildirimSeverityleri
{
    public const string Success = "success";
    public const string Info = "info";
    public const string Warn = "warn";
    public const string Error = "error";
    public const string Danger = "danger";

    public static readonly IReadOnlyList<string> TumDegerler = [Info, Success, Warn, Error, Danger];

    public static string Normalize(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            return Info;
        }

        var normalized = severity.Trim().ToLowerInvariant();
        return normalized switch
        {
            Success => Success,
            Info => Info,
            Warn => Warn,
            "warning" => Warn,
            Error => Error,
            Danger => Danger,
            _ => Info
        };
    }

    public static bool IsAtLeast(string incomingSeverity, string minimumSeverity)
    {
        return Rank(Normalize(incomingSeverity)) >= Rank(Normalize(minimumSeverity));
    }

    private static int Rank(string severity)
    {
        return severity switch
        {
            Info => 0,
            Success => 1,
            Warn => 2,
            Error => 3,
            Danger => 4,
            _ => 0
        };
    }
}
