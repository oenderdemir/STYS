using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TOD.Platform.Identity.Roles.Controllers;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Infrastructure.Stores;
using TOD.Platform.Persistence.Extensions;
using TOD.Platform.Security;

namespace TOD.Platform.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddTodPlatformIdentity(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        IConfiguration? configuration = null)
    {
        services.AddDbContext<TodIdentityDbContext>(configureDbContext);

        services.AddTodPlatformSecurity(configuration);
        services.AddTodPlatformAuthentication<EfIdentityStore>();

        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(DependencyInjection).Assembly);
        }, NullLoggerFactory.Instance);
        services.AddSingleton(mapperConfig);
        services.AddScoped<IMapper>(sp => sp.GetRequiredService<MapperConfiguration>().CreateMapper(sp.GetService));

        services.AddBaseServicesAndRepositoriesScoped(typeof(DependencyInjection).Assembly);

        services.AddControllers()
            .AddApplicationPart(typeof(RoleController).Assembly);

        return services;
    }
}
