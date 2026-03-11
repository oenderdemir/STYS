namespace STYS.Bildirimler;

public static class BildirimSeverityleri
{
    public const string Success = "success";
    public const string Info = "info";
    public const string Warn = "warn";
    public const string Error = "error";
    public const string Danger = "danger";

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
}

