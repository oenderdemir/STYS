using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TOD.Platform.Security.Auth.Options;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddTodPlatformSecurity(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        if (configuration is not null)
        {
            services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));
        }
        else
        {
            services.AddOptions<JwtTokenOptions>();
        }

        return services;
    }

    public static IServiceCollection AddTodPlatformAuthentication<TIdentityStore>(this IServiceCollection services)
        where TIdentityStore : class, IIdentityStore<Guid>
    {
        services.AddScoped<IIdentityStore<Guid>, TIdentityStore>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IAuthenticationService<Guid>>(sp => sp.GetRequiredService<IAuthenticationService>());

        return services;
    }

    public static IServiceCollection AddTodPlatformAuthentication<TKey, TIdentityStore>(this IServiceCollection services)
        where TKey : struct
        where TIdentityStore : class, IIdentityStore<TKey>
    {
        services.AddScoped<IIdentityStore<TKey>, TIdentityStore>();
        services.AddScoped<IAuthenticationService<TKey>, AuthenticationService<TKey>>();

        return services;
    }
}
