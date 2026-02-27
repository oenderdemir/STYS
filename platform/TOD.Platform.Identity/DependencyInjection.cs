using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TOD.Platform.Identity.Roles.Controllers;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Infrastructure.Stores;
using TOD.Platform.Identity.MenuItemRoles.Repositories;
using TOD.Platform.Identity.MenuItemRoles.Services;
using TOD.Platform.Identity.MenuItems.Repositories;
using TOD.Platform.Identity.MenuItems.Services;
using TOD.Platform.Identity.Roles.Repositories;
using TOD.Platform.Identity.Roles.Services;
using TOD.Platform.Identity.UserGroupRoles.Repositories;
using TOD.Platform.Identity.UserGroupRoles.Services;
using TOD.Platform.Identity.UserGroups.Repositories;
using TOD.Platform.Identity.UserGroups.Services;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Identity.UserUserGroups.Repositories;
using TOD.Platform.Identity.UserUserGroups.Services;
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

        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserGroupRepository, UserGroupRepository>();
        services.AddScoped<IUserGroupRoleRepository, UserGroupRoleRepository>();
        services.AddScoped<IUserUserGroupRepository, UserUserGroupRepository>();
        services.AddScoped<IMenuItemRepository, MenuItemRepository>();
        services.AddScoped<IMenuItemRoleRepository, MenuItemRoleRepository>();

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserGroupService, UserGroupService>();
        services.AddScoped<IUserGroupRoleService, UserGroupRoleService>();
        services.AddScoped<IUserUserGroupService, UserUserGroupService>();
        services.AddScoped<IMenuItemService, MenuItemService>();
        services.AddScoped<IMenuItemRoleService, MenuItemRoleService>();

        services.AddControllers()
            .AddApplicationPart(typeof(RoleController).Assembly);

        return services;
    }
}
