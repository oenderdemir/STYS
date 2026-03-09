using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.AspNetCore.Authorization;

public static class TodPlatformJwtAuthenticationExtensions
{
    public static IServiceCollection AddTodPlatformJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var jwtKey = Environment.GetEnvironmentVariable("Jwt__Key") ?? jwtSection["Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("Jwt:Key is required. Configure it through environment/secret manager (Jwt__Key).");
        }

        if (jwtKey.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        }

        if (!isDevelopment && jwtKey.Contains("ChangeMe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Jwt:Key uses a placeholder value. Use a secure secret manager value in production.");
        }

        var jwtIssuer = jwtSection["Issuer"];
        var jwtAudience = jwtSection["Audience"];
        var validateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer);
        var validateAudience = !string.IsNullOrWhiteSpace(jwtAudience);

        void ConfigureJwt(JwtBearerOptions options)
        {
            options.RequireHttpsMetadata = !isDevelopment;
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuer = validateIssuer,
                ValidateAudience = validateAudience,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var rawUserId = context.Principal?.FindFirstValue("userId");
                    if (!Guid.TryParse(rawUserId, out var userId))
                    {
                        context.Fail("Missing or invalid userId claim.");
                        return;
                    }

                    var rawTokenVersion = context.Principal?.FindFirstValue("tokenVersion");
                    if (!int.TryParse(rawTokenVersion, out var tokenVersion))
                    {
                        context.Fail("Missing or invalid tokenVersion claim.");
                        return;
                    }

                    var identityStore = context.HttpContext.RequestServices.GetService<IIdentityStore<Guid>>();
                    if (identityStore is null)
                    {
                        context.Fail("Identity store is not available.");
                        return;
                    }

                    var currentTokenVersion = await identityStore.GetTokenVersionAsync(userId, context.HttpContext.RequestAborted);
                    if (!currentTokenVersion.HasValue || currentTokenVersion.Value != tokenVersion)
                    {
                        context.Fail("Token version is no longer valid.");
                    }
                }
            };
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, ConfigureJwt)
            .AddJwtBearer(TodPlatformAuthorizationConstants.UiScheme, ConfigureJwt)
            .AddJwtBearer(TodPlatformAuthorizationConstants.ServiceScheme, ConfigureJwt);

        return services;
    }
}
