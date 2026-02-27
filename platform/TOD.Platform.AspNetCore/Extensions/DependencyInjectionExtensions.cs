using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace TOD.Platform.AspNetCore.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServicesAndRepositoriesFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.GetTypes();
        var interfaces = types.Where(t => t.IsInterface);
        var classes = types.Where(t => t.IsClass && !t.IsAbstract);

        foreach (var @interface in interfaces)
        {
            var implementation = classes.FirstOrDefault(c => @interface.IsAssignableFrom(c));

            if (implementation is not null)
            {
                services.AddScoped(@interface, implementation);
            }
        }

        return services;
    }
}
