using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TOD.Platform.AspNetCore.CurrentUser.Services;
using TOD.Platform.AspNetCore.Filters;
using TOD.Platform.SharedKernel.Responses;
using TOD.Platform.SharedKernel.Exceptions;

namespace TOD.Platform.AspNetCore;

public static class TodPlatformExtensions
{
    public static IServiceCollection AddTodPlatformDefaults(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddTodPlatformHealthChecks();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add(new ApiResponseWrapperFilter());
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value!.Errors.Select(e =>
                        new ApiError("VALIDATION_ERROR", x.Key, string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Validation failed." : e.ErrorMessage)))
                    .ToList();

                return new BadRequestObjectResult(ApiResponse.Fail("Validation failed.", errors, context.HttpContext.TraceIdentifier));
            };
        });

        return services;
    }

    public static IServiceCollection AddTodPlatformHealthChecks(
        this IServiceCollection services,
        Action<IHealthChecksBuilder>? configure = null)
    {
        var builder = services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy("Application is running."),
                tags: ["live", "ready"]);

        configure?.Invoke(builder);

        return services;
    }

    public static IApplicationBuilder UseTodPlatformDefaults(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var (statusCode, code, message) = ResolveException(exception);

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(message, [new ApiError(code, null, message)], context.TraceIdentifier));
            });
        });

        app.UseStatusCodePages(statusCodeApp =>
        {
            statusCodeApp.Run(async context =>
            {
                var response = context.Response;
                if (response.StatusCode < 400)
                {
                    return;
                }

                var message = ReasonPhrases.GetReasonPhrase(response.StatusCode);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "Request failed.";
                }

                response.ContentType = "application/json";
                await response.WriteAsJsonAsync(ApiResponse.Fail(message, [new ApiError($"HTTP_{response.StatusCode}", null, message)], context.TraceIdentifier));
            });
        });

        return app;
    }

    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<Middleware.RequestResponseLoggingMiddleware>();
    }

    public static IApplicationBuilder UseJwtTokenLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<Middleware.JwtTokenLoggingMiddleware>();
    }

    public static WebApplication MapTodPlatformHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthCheckResponse
        }).AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthCheckResponse
        }).AllowAnonymous();

        return app;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["X-Permitted-Cross-Domain-Policies"] = "none";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                if (context.Request.IsHttps)
                {
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static (int StatusCode, string Code, string Message) ResolveException(Exception? exception)
    {
        if (exception is null)
        {
            return (500, "UNEXPECTED_ERROR", "An unexpected error occurred.");
        }

        if (exception is BaseException baseException)
        {
            var statusCode = baseException.ErrorCode is >= 400 and <= 599 ? baseException.ErrorCode : 500;
            var message = string.IsNullOrWhiteSpace(baseException.Message) ? "Request failed." : baseException.Message;
            return (statusCode, "BASE_EXCEPTION", message);
        }

        if (exception is UnauthorizedAccessException)
        {
            return (401, "UNAUTHORIZED", "Unauthorized access.");
        }

        return (500, "UNEXPECTED_ERROR", string.IsNullOrWhiteSpace(exception.Message) ? "An unexpected error occurred." : exception.Message);
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                x => x.Key,
                x => new
                {
                    status = x.Value.Status.ToString(),
                    description = x.Value.Description,
                    durationMs = x.Value.Duration.TotalMilliseconds
                })
        };

        if (report.Status == HealthStatus.Unhealthy)
        {
            var errors = report.Entries
                .Where(x => x.Value.Status == HealthStatus.Unhealthy)
                .Select(x => new ApiError("HEALTHCHECK_UNHEALTHY", x.Key, x.Value.Description ?? "Health check failed."))
                .ToList();

            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail("Health check failed.", errors, context.TraceIdentifier));
            return;
        }

        await context.Response.WriteAsJsonAsync(
            ApiResponse.Ok(payload, "Health check completed.", context.TraceIdentifier));
    }
}
