using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TOD.Platform.AspNetCore.Logging;

public class DomainOperationLogger : IDomainOperationLogger
{
    private readonly ILogger<DomainOperationLogger> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DomainOperationLogger(
        ILogger<DomainOperationLogger> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Started(string eventName, object payload)
        => _logger.LogInformation("Domain event started {@DomainEvent}", BuildEntry("Started", eventName, payload));

    public void Completed(string eventName, object payload)
        => _logger.LogInformation("Domain event completed {@DomainEvent}", BuildEntry("Completed", eventName, payload));

    public void Warning(string eventName, object payload)
        => _logger.LogWarning("Domain event warning {@DomainEvent}", BuildEntry("Warning", eventName, payload));

    public void Failed(string eventName, Exception exception, object payload)
        => _logger.LogError(exception, "Domain event failed {@DomainEvent}", BuildEntry("Failed", eventName, payload));

    private object BuildEntry(string eventType, string eventName, object payload)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var user = ctx?.User;
        var userId = user?.FindFirstValue("userId") ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user?.FindFirstValue("userName") ?? user?.Identity?.Name;
        var traceId = ctx?.TraceIdentifier;
        var requestGuid = ctx?.Items.TryGetValue("RequestGuid", out var rg) == true ? rg?.ToString() : null;

        return new
        {
            EventType = eventType,
            EventName = eventName,
            TimestampUtc = DateTime.UtcNow,
            TraceId = traceId,
            RequestGuid = requestGuid,
            UserId = userId,
            UserName = userName,
            Payload = payload ?? new { }
        };
    }
}
