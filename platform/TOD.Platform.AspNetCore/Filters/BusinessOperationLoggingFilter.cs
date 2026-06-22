using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TOD.Platform.AspNetCore.Filters;

public sealed class BusinessOperationLoggingFilter : IAsyncActionFilter
{
    private const int MaxSerializedLength = 6_000;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<BusinessOperationLoggingFilter> _logger;
    private readonly bool _logReadOperations;
    private readonly bool _logActionArguments;

    public BusinessOperationLoggingFilter(
        ILogger<BusinessOperationLoggingFilter> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _logReadOperations = configuration.GetValue("BusinessOperationLogging:LogReadOperations", false);
        _logActionArguments = configuration.GetValue("BusinessOperationLogging:LogActionArguments", true);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        var httpContext = context.HttpContext;
        var request = httpContext.Request;
        var method = request.Method;
        var isReadOperation = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        var shouldWriteInfoLog = !isReadOperation || _logReadOperations;
        var operationName = ResolveOperationName(context);
        var stopwatch = Stopwatch.StartNew();

        var commonLogData = new
        {
            Type = "BusinessOperation",
            OperationName = operationName,
            Controller = context.RouteData.Values["controller"]?.ToString(),
            Action = context.RouteData.Values["action"]?.ToString(),
            Area = context.RouteData.Values["area"]?.ToString(),
            Method = method,
            Path = request.Path.Value,
            QueryString = request.QueryString.Value,
            TraceId = httpContext.TraceIdentifier,
            RequestGuid = httpContext.Items.TryGetValue("RequestGuid", out var requestGuid) ? requestGuid : null,
            UserId = GetClaimValue(context, ClaimTypes.NameIdentifier) ?? GetClaimValue(context, "sub"),
            UserName = GetClaimValue(context, "userName") ?? httpContext.User.Identity?.Name,
            KurumId = GetClaimValue(context, "kurumId"),
            AktifKurumId = GetClaimValue(context, "aktifKurumId") ?? request.Headers["X-Aktif-Kurum-Id"].FirstOrDefault(),
            TesisId = TryGetActionArgument(context, "tesisId"),
            RezervasyonId = TryGetActionArgument(context, "rezervasyonId"),
            EntityId = TryGetActionArgument(context, "id"),
            Arguments = _logActionArguments ? SerializeArguments(context.ActionArguments) : null
        };

        if (shouldWriteInfoLog)
        {
            _logger.LogInformation("Business operation started {@BusinessOperation}", commonLogData);
        }
        else
        {
            _logger.LogDebug("Business operation started {@BusinessOperation}", commonLogData);
        }

        ActionExecutedContext executedContext;
        try
        {
            executedContext = await next();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Business operation failed {@BusinessOperation}",
                new
                {
                    commonLogData.Type,
                    commonLogData.OperationName,
                    commonLogData.Controller,
                    commonLogData.Action,
                    commonLogData.Method,
                    commonLogData.Path,
                    commonLogData.TraceId,
                    commonLogData.RequestGuid,
                    commonLogData.UserId,
                    commonLogData.UserName,
                    commonLogData.KurumId,
                    commonLogData.AktifKurumId,
                    commonLogData.TesisId,
                    commonLogData.RezervasyonId,
                    commonLogData.EntityId,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    ExceptionType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            throw;
        }

        stopwatch.Stop();
        var statusCode = ResolveStatusCode(executedContext);
        var resultSummary = ResolveResultSummary(executedContext.Result);
        var completedLogData = new
        {
            commonLogData.Type,
            commonLogData.OperationName,
            commonLogData.Controller,
            commonLogData.Action,
            commonLogData.Method,
            commonLogData.Path,
            commonLogData.TraceId,
            commonLogData.RequestGuid,
            commonLogData.UserId,
            commonLogData.UserName,
            commonLogData.KurumId,
            commonLogData.AktifKurumId,
            commonLogData.TesisId,
            commonLogData.RezervasyonId,
            commonLogData.EntityId,
            StatusCode = statusCode,
            DurationMs = stopwatch.ElapsedMilliseconds,
            ResultType = resultSummary.ResultType,
            ResultValueType = resultSummary.ResultValueType
        };

        if (executedContext.Exception is not null)
        {
            _logger.LogError(executedContext.Exception,
                "Business operation failed {@BusinessOperation}",
                completedLogData);
            return;
        }

        if (statusCode >= 500)
        {
            _logger.LogError("Business operation completed with server error {@BusinessOperation}", completedLogData);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning("Business operation completed with client error {@BusinessOperation}", completedLogData);
        }
        else if (stopwatch.ElapsedMilliseconds > 5_000)
        {
            _logger.LogWarning("Business operation completed slowly {@BusinessOperation}", completedLogData);
        }
        else if (shouldWriteInfoLog)
        {
            _logger.LogInformation("Business operation completed {@BusinessOperation}", completedLogData);
        }
        else
        {
            _logger.LogDebug("Business operation completed {@BusinessOperation}", completedLogData);
        }
    }

    private static bool ShouldSkip(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path;
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveOperationName(ActionExecutingContext context)
    {
        var controller = context.RouteData.Values["controller"]?.ToString();
        var action = context.RouteData.Values["action"]?.ToString();
        var method = context.HttpContext.Request.Method;

        if (!string.IsNullOrWhiteSpace(controller) && !string.IsNullOrWhiteSpace(action))
        {
            return $"{controller}.{action}";
        }

        return $"{method} {context.HttpContext.Request.Path.Value}";
    }

    private static string? GetClaimValue(ActionContext context, string claimType)
    {
        return context.HttpContext.User.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
    }

    private static object? TryGetActionArgument(ActionExecutingContext context, string key)
    {
        if (context.ActionArguments.TryGetValue(key, out var value))
        {
            return MaskValue(key, value);
        }

        var routeValue = context.RouteData.Values.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        if (routeValue is not null)
        {
            return routeValue.ToString();
        }

        var queryValue = context.HttpContext.Request.Query.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)).Value.FirstOrDefault();
        return queryValue;
    }

    private static string? SerializeArguments(IDictionary<string, object?> actionArguments)
    {
        if (actionArguments.Count == 0)
        {
            return null;
        }

        try
        {
            var maskedArguments = actionArguments.ToDictionary(
                x => x.Key,
                x => MaskValue(x.Key, x.Value));

            var serialized = JsonSerializer.Serialize(maskedArguments, SerializerOptions);
            return serialized.Length <= MaxSerializedLength
                ? serialized
                : $"{serialized[..MaxSerializedLength]}... [truncated, originalLength={serialized.Length}]";
        }
        catch (Exception ex)
        {
            return $"Action arguments could not be serialized: {ex.GetType().Name}";
        }
    }

    private static object? MaskValue(string key, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (IsSensitive(key))
        {
            return "***";
        }

        if (value is string text)
        {
            if (IsLikelySensitiveFreeText(key))
            {
                return "***";
            }

            return text.Length <= 256 ? text : $"{text[..256]}... [truncated, originalLength={text.Length}]";
        }

        if (value.GetType().IsPrimitive || value is decimal || value is DateTime || value is DateOnly || value is TimeOnly || value is Guid || value is Enum)
        {
            return value;
        }

        return MaskComplexObject(value);
    }

    private static object? MaskComplexObject(object value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, SerializerOptions);
            using var document = JsonDocument.Parse(json);
            var masked = MaskElement(document.RootElement);
            return masked;
        }
        catch
        {
            return new
            {
                Type = value.GetType().Name,
                SerializationSkipped = true
            };
        }
    }

    private static object? MaskElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                p => p.Name,
                p => IsSensitive(p.Name) || IsLikelySensitiveFreeText(p.Name) ? "***" : MaskElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Take(50).Select(MaskElement).ToList(),
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
        return key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("parola", StringComparison.OrdinalIgnoreCase)
            || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
            || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || key.Contains("api_key", StringComparison.OrdinalIgnoreCase)
            || key.Contains("clientsecret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("client_secret", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelySensitiveFreeText(string propertyName)
    {
        var key = propertyName.ToLowerInvariant();
        return key.Contains("tckimlik", StringComparison.OrdinalIgnoreCase)
            || key.Contains("tcKimlik", StringComparison.OrdinalIgnoreCase)
            || key.Contains("kimlikno", StringComparison.OrdinalIgnoreCase)
            || key.Contains("pasaport", StringComparison.OrdinalIgnoreCase)
            || key.Contains("telefon", StringComparison.OrdinalIgnoreCase)
            || key.Contains("eposta", StringComparison.OrdinalIgnoreCase)
            || key.Contains("email", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveStatusCode(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult objectResult && objectResult.StatusCode.HasValue)
        {
            return objectResult.StatusCode.Value;
        }

        if (context.Result is StatusCodeResult statusCodeResult)
        {
            return statusCodeResult.StatusCode;
        }

        return context.HttpContext.Response.StatusCode;
    }

    private static (string? ResultType, string? ResultValueType) ResolveResultSummary(IActionResult? result)
    {
        if (result is null)
        {
            return (null, null);
        }

        return result switch
        {
            ObjectResult objectResult => (result.GetType().Name, objectResult.Value?.GetType().Name),
            JsonResult jsonResult => (result.GetType().Name, jsonResult.Value?.GetType().Name),
            _ => (result.GetType().Name, null)
        };
    }
}
