using Microsoft.AspNetCore.Http;

namespace TOD.Platform.AspNetCore.RateLimiting;

public static class TodPlatformRateLimitingConstants
{
    public const string SectionName = "TodPlatform:RateLimiting";

    public const bool EnabledDefault = true;
    public const int PermitLimitDefault = 120;
    public const int WindowSecondsDefault = 60;
    public const int QueueLimitDefault = 0;
    public const int RejectionStatusCodeDefault = StatusCodes.Status429TooManyRequests;

    public const string RejectionMessageDefault = "Too many requests.";
    public const string RejectionErrorCodeDefault = "RATE_LIMITED";
    public const string RejectionDetailDefault = "Too many requests. Please try again later.";
    public const string UnknownClientIpDefault = "unknown";
}
