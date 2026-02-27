using System.Threading.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace TOD.Platform.AspNetCore.RateLimiting;

public sealed class TodPlatformRateLimitingOptions
{
    private const string EnabledKey = "Enabled";
    private const string PermitLimitKey = "PermitLimit";
    private const string WindowSecondsKey = "WindowSeconds";
    private const string QueueLimitKey = "QueueLimit";
    private const string QueueProcessingOrderKey = "QueueProcessingOrder";
    private const string RejectionStatusCodeKey = "RejectionStatusCode";
    private const string RejectionMessageKey = "RejectionMessage";
    private const string RejectionErrorCodeKey = "RejectionErrorCode";
    private const string RejectionDetailKey = "RejectionDetail";
    private const string UnknownClientIpKey = "UnknownClientIp";

    public bool Enabled { get; set; } = TodPlatformRateLimitingConstants.EnabledDefault;
    public int PermitLimit { get; set; } = TodPlatformRateLimitingConstants.PermitLimitDefault;
    public int WindowSeconds { get; set; } = TodPlatformRateLimitingConstants.WindowSecondsDefault;
    public int QueueLimit { get; set; } = TodPlatformRateLimitingConstants.QueueLimitDefault;
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;
    public int RejectionStatusCode { get; set; } = TodPlatformRateLimitingConstants.RejectionStatusCodeDefault;
    public string RejectionMessage { get; set; } = TodPlatformRateLimitingConstants.RejectionMessageDefault;
    public string RejectionErrorCode { get; set; } = TodPlatformRateLimitingConstants.RejectionErrorCodeDefault;
    public string RejectionDetail { get; set; } = TodPlatformRateLimitingConstants.RejectionDetailDefault;
    public string UnknownClientIp { get; set; } = TodPlatformRateLimitingConstants.UnknownClientIpDefault;

    public static TodPlatformRateLimitingOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(TodPlatformRateLimitingConstants.SectionName);
        var options = new TodPlatformRateLimitingOptions
        {
            Enabled = Parse(section[EnabledKey], TodPlatformRateLimitingConstants.EnabledDefault),
            PermitLimit = Parse(section[PermitLimitKey], TodPlatformRateLimitingConstants.PermitLimitDefault),
            WindowSeconds = Parse(section[WindowSecondsKey], TodPlatformRateLimitingConstants.WindowSecondsDefault),
            QueueLimit = Parse(section[QueueLimitKey], TodPlatformRateLimitingConstants.QueueLimitDefault),
            RejectionStatusCode = Parse(section[RejectionStatusCodeKey], TodPlatformRateLimitingConstants.RejectionStatusCodeDefault),
            RejectionMessage = section[RejectionMessageKey] ?? TodPlatformRateLimitingConstants.RejectionMessageDefault,
            RejectionErrorCode = section[RejectionErrorCodeKey] ?? TodPlatformRateLimitingConstants.RejectionErrorCodeDefault,
            RejectionDetail = section[RejectionDetailKey] ?? TodPlatformRateLimitingConstants.RejectionDetailDefault,
            UnknownClientIp = section[UnknownClientIpKey] ?? TodPlatformRateLimitingConstants.UnknownClientIpDefault
        };

        var queueProcessingOrderValue = section[QueueProcessingOrderKey];
        if (!string.IsNullOrWhiteSpace(queueProcessingOrderValue) &&
            Enum.TryParse<QueueProcessingOrder>(queueProcessingOrderValue, true, out var parsedQueueProcessingOrder))
        {
            options.QueueProcessingOrder = parsedQueueProcessingOrder;
        }

        return options;
    }

    private static bool Parse(string? value, bool defaultValue) =>
        bool.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;

    private static int Parse(string? value, int defaultValue) =>
        int.TryParse(value, out var parsedValue) ? parsedValue : defaultValue;
}
