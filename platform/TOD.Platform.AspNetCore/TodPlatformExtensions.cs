using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
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
}
