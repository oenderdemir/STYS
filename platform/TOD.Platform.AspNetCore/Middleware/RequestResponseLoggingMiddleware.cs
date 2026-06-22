using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace TOD.Platform.AspNetCore.Middleware;

public class RequestResponseLoggingMiddleware
{
    private const int MaxLoggedBodyLength = 8_192;
    private static readonly string[] SkippedBodyPathPrefixes =
    [
        "/health",
        "/swagger",
        "/hangfire",
        "/hubs"
    ];

    private readonly RequestDelegate _next;

    public RequestResponseLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var stopWatch = Stopwatch.StartNew();
        var requestGuid = Guid.NewGuid();

        context.Items["RequestGuid"] = requestGuid;

        using var traceScope = LogContext.PushProperty("TraceId", context.TraceIdentifier);
        using var requestScope = LogContext.PushProperty("RequestGuid", requestGuid);

        var originalBodyStream = context.Response.Body;
        await using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            var requestBody = await TryReadRequestBodyAsync(context.Request);
            LogRequest(context, requestGuid, requestBody);

            await _next(context);

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await TryReadResponseBodyAsync(context.Response, responseBodyStream, context.Request.Path);
            LogResponse(context, requestGuid, responseBody);

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            context.Response.Body = originalBodyStream;
            Log.Error(ex,
                "Unhandled exception for request {RequestGuid}. {Method} {Path}{QueryString}",
                requestGuid,
                context.Request.Method,
                context.Request.Path.Value,
                context.Request.QueryString.Value);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            stopWatch.Stop();
            LogElapsedTime(context, requestGuid, stopWatch.ElapsedMilliseconds);
        }
    }

    private static async Task<string?> TryReadRequestBodyAsync(HttpRequest request)
    {
        if (!CanLogBody(request.ContentType, request.Path))
        {
            return null;
        }

        request.EnableBuffering();
        request.Body.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        return MaskSensitiveData(Truncate(text));
    }

    private static async Task<string?> TryReadResponseBodyAsync(HttpResponse response, Stream responseBodyStream, PathString path)
    {
        if (!CanLogBody(response.ContentType, path))
        {
            return null;
        }

        var text = await new StreamReader(responseBodyStream, leaveOpen: true).ReadToEndAsync();
        return MaskSensitiveData(Truncate(text));
    }

    private static bool CanLogBody(string? contentType, PathString path)
    {
        if (SkippedBodyPathPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("text", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("form", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string body)
    {
        if (string.IsNullOrEmpty(body) || body.Length <= MaxLoggedBodyLength)
        {
            return body;
        }

        return $"{body[..MaxLoggedBodyLength]}... [truncated, originalLength={body.Length}]";
    }

    private static string? MaskSensitiveData(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var masked = MaskElement(document.RootElement);
            return JsonSerializer.Serialize(masked);
        }
        catch
        {
            return body;
        }
    }

    private static object? MaskElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                p => p.Name,
                p => IsSensitive(p.Name) ? "***" : MaskElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(MaskElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static bool IsSensitive(string propertyName)
    {
        var key = propertyName.ToLowerInvariant();
        return key is "password" or "parola" or "secret" or "token" or "accesstoken" or "refreshtoken" or "authtoken" or "authorization" or "apikey" or "api_key" or "clientsecret" or "client_secret";
    }

    private static void LogRequest(HttpContext context, Guid requestGuid, string? body)
    {
        var logData = new
        {
            Type = "Request",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.Value,
            Path = context.Request.Path.Value,
            Method = context.Request.Method,
            QueryString = context.Request.QueryString.ToString(),
            ContentType = context.Request.ContentType,
            ContentLength = context.Request.ContentLength,
            RequestBody = body,
            ClientIP = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        Log.Information("HTTP request started {@LogData}", logData);
    }

    private static void LogResponse(HttpContext context, Guid requestGuid, string? responseBody)
    {
        var statusCode = context.Response.StatusCode;
        var logData = new
        {
            Type = "Response",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            StatusCode = statusCode,
            ContentType = context.Response.ContentType,
            ContentLength = context.Response.ContentLength,
            UserId = GetClaimValue(context, ClaimTypes.NameIdentifier) ?? GetClaimValue(context, "sub"),
            UserName = GetClaimValue(context, "userName") ?? context.User.Identity?.Name,
            KurumId = GetClaimValue(context, "kurumId"),
            ResponseBody = responseBody
        };

        var level = statusCode >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : statusCode >= StatusCodes.Status400BadRequest
                ? LogEventLevel.Warning
                : LogEventLevel.Information;

        Log.Write(level, "HTTP request completed {@LogData}", logData);
    }

    private static void LogElapsedTime(HttpContext context, Guid requestGuid, long elapsedTime)
    {
        var logData = new
        {
            Type = "ExecutionTime",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            TraceId = context.TraceIdentifier,
            Path = context.Request.Path.Value,
            Method = context.Request.Method,
            StatusCode = context.Response.StatusCode,
            ElapsedTimeMs = elapsedTime
        };

        var level = elapsedTime > 5_000
            ? LogEventLevel.Warning
            : LogEventLevel.Information;

        Log.Write(level, "HTTP request execution time {@LogData}", logData);
    }

    private static string? GetClaimValue(HttpContext context, string claimType)
    {
        return context.User.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
    }
}
