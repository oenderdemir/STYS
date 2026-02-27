using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text.Json;
using Serilog;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.AspNetCore.Middleware;

public class RequestResponseLoggingMiddleware
{
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
        context.Request.EnableBuffering();

        var originalBodyStream = context.Response.Body;
        await using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            var body = await ReadRequestBody(context.Request);
            var maskedBody = MaskSensitiveData(body);

            LogRequest(context, requestGuid, maskedBody);

            context.Request.Body.Position = 0;
            await _next(context);

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            LogResponse(requestGuid, responseBody);

            responseBodyStream.Seek(0, SeekOrigin.Begin);
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            context.Response.Body = originalBodyStream;
            await HandleException(context, ex);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
            stopWatch.Stop();
            LogElapsedTime(requestGuid, stopWatch.ElapsedMilliseconds);
        }
    }

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        var text = await new StreamReader(request.Body).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);

        return text;
    }

    private static string MaskSensitiveData(string body)
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
        return key is "password" or "parola" or "secret" or "token";
    }

    private static void LogRequest(HttpContext context, Guid requestGuid, string body)
    {
        var logData = new
        {
            Type = "Request",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            Schema = context.Request.Scheme,
            Host = context.Request.Host.Value,
            Path = context.Request.Path,
            Method = context.Request.Method,
            QueryString = context.Request.QueryString.ToString(),
            RequestBody = body,
            ClientIP = context.Connection.RemoteIpAddress?.ToString()
        };

        Log.Information("{@LogData}", logData);
    }

    private static void LogResponse(Guid requestGuid, string responseBody)
    {
        var logData = new
        {
            Type = "Response",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            ResponseBody = responseBody
        };

        Log.Information("{@LogData}", logData);
    }

    private static void LogElapsedTime(Guid requestGuid, long elapsedTime)
    {
        var logData = new
        {
            Type = "ExecutionTime",
            RequestGuid = requestGuid,
            Timestamp = DateTime.UtcNow,
            ElapsedTime = elapsedTime
        };

        Log.Information("{@LogData}", logData);
    }

    private static Task HandleException(HttpContext context, Exception ex)
    {
        var requestGuid = context.Items["RequestGuid"] as Guid? ?? Guid.Empty;
        var code = "system_error";
        var statusCode = 500;

        if (ex is BaseException baseException)
        {
            code = baseException.ErrorCode.ToString();
            statusCode = baseException.ErrorCode;
        }

        var errorBody = JsonSerializer.Serialize(new { Message = ex.Message, Code = code });

        Log.Error(ex, "Unhandled exception for request {RequestGuid}", requestGuid);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        return context.Response.WriteAsync(errorBody);
    }
}
