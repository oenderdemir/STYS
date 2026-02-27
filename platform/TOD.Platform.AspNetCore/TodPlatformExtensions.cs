using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using TOD.Platform.AspNetCore.CurrentUser.Services;

namespace TOD.Platform.AspNetCore;

public static class TodPlatformExtensions
{
    public static IServiceCollection AddTodPlatformDefaults(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }

    public static IApplicationBuilder UseTodPlatformDefaults(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();

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
}
