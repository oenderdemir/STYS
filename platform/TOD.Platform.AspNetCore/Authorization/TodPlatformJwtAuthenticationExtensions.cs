using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace TOD.Platform.AspNetCore.Authorization;

public static class TodPlatformJwtAuthenticationExtensions
{
    public static IServiceCollection AddTodPlatformJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        var jwtIssuer = jwtSection["Issuer"];
        var jwtAudience = jwtSection["Audience"];
        var validateIssuer = !string.IsNullOrWhiteSpace(jwtIssuer);
        var validateAudience = !string.IsNullOrWhiteSpace(jwtAudience);

        void ConfigureJwt(JwtBearerOptions options)
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
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
