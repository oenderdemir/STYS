using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;
using STYS.Agent.Modules.Pavo.Commands;

namespace STYS.Agent.Modules.Pavo;

public static class PavoModuleExtensions
{
    public static IServiceCollection AddPavoModule(this IServiceCollection services)
    {
        services.AddScoped<PavoConnectionTestCommandHandler>();
        return services;
    }

    public static void RegisterPavoCommands(AgentCommandHandlerRegistry registry)
    {
        registry.Register<PavoConnectionTestCommand, PavoConnectionTestCommandHandler>("PavoConnectionTest");
    }
}
