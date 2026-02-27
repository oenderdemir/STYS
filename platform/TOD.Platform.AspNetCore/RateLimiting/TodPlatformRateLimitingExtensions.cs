using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TOD.Platform.SharedKernel.Responses;

namespace TOD.Platform.AspNetCore.RateLimiting;

public static class TodPlatformRateLimitingExtensions
{
    public static IServiceCollection AddTodPlatformRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TodPlatformRateLimitingOptions>? configure = null)
    {
        var options = TodPlatformRateLimitingOptions.FromConfiguration(configuration);
        configure?.Invoke(options);
        Validate(options);

        services.AddSingleton(options);

        if (!options.Enabled)
        {
            return services;
        }

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = options.RejectionStatusCode;
            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? options.UnknownClientIp;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueProcessingOrder = options.QueueProcessingOrder,
                        QueueLimit = options.QueueLimit
                    });
            });

            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(
                        options.RejectionMessage,
                        [new ApiError(options.RejectionErrorCode, null, options.RejectionDetail)],
                        context.HttpContext.TraceIdentifier),
                    cancellationToken: cancellationToken);
            };
        });

        return services;
    }

    public static IApplicationBuilder UseTodPlatformRateLimiting(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<TodPlatformRateLimitingOptions>();
        if (!options.Enabled)
        {
            return app;
        }

        app.UseRateLimiter();
        return app;
    }

    private static void Validate(TodPlatformRateLimitingOptions options)
    {
        if (options.PermitLimit <= 0)
        {
            throw new InvalidOperationException("TodPlatform rate limiting: PermitLimit must be greater than zero.");
        }

        if (options.WindowSeconds <= 0)
        {
            throw new InvalidOperationException("TodPlatform rate limiting: WindowSeconds must be greater than zero.");
        }

        if (options.QueueLimit < 0)
        {
            throw new InvalidOperationException("TodPlatform rate limiting: QueueLimit cannot be negative.");
        }

        if (options.RejectionStatusCode < 400 || options.RejectionStatusCode > 599)
        {
            throw new InvalidOperationException("TodPlatform rate limiting: RejectionStatusCode must be between 400 and 599.");
        }

        if (string.IsNullOrWhiteSpace(options.RejectionMessage))
        {
            throw new InvalidOperationException("TodPlatform rate limiting: RejectionMessage is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RejectionErrorCode))
        {
            throw new InvalidOperationException("TodPlatform rate limiting: RejectionErrorCode is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RejectionDetail))
        {
            throw new InvalidOperationException("TodPlatform rate limiting: RejectionDetail is required.");
        }
    }
}
