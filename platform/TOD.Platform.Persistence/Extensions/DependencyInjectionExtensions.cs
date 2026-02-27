using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TOD.Platform.Persistence.RDBMS.Repositories;
using TOD.Platform.Persistence.RDBMS.Services;

namespace TOD.Platform.Persistence.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddBaseServicesAndRepositoriesScoped(
        this IServiceCollection services,
        Assembly assembly)
    {
        var implementations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var implementation in implementations)
        {
            var contracts = implementation.GetInterfaces()
                .Where(i => i.Assembly == assembly)
                .Where(IsRdbmsContract)
                .ToList();

            foreach (var contract in contracts)
            {
                services.TryAddScoped(contract, implementation);
            }
        }

        return services;
    }

    private static bool IsRdbmsContract(Type contract)
    {
        return IsAssignableToOpenGeneric(contract, typeof(IBaseRepository<>)) ||
               IsAssignableToOpenGeneric(contract, typeof(IBaseRepository<,>)) ||
               IsAssignableToOpenGeneric(contract, typeof(IBaseService<,>)) ||
               IsAssignableToOpenGeneric(contract, typeof(IBaseService<,,>));
    }

    private static bool IsAssignableToOpenGeneric(Type type, Type openGenericType)
    {
        if (!openGenericType.IsGenericTypeDefinition)
        {
            return false;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == openGenericType)
        {
            return true;
        }

        foreach (var implementedInterface in type.GetInterfaces())
        {
            if (implementedInterface.IsGenericType &&
                implementedInterface.GetGenericTypeDefinition() == openGenericType)
            {
                return true;
            }
        }

        return false;
    }
}
