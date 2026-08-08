using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;
using STYS.Agent.Modules.Pavo.Commands;

namespace STYS.Agent.Modules.Pavo;

public static class PavoModuleExtensions
{
    public static IServiceCollection AddPavoModule(this IServiceCollection services)
    {
        services.AddScoped<PavoConnectionTestCommandHandler>();
        services.AddHttpClient("PavoClient");
        services.AddScoped<IPavoClient, PavoHttpClient>();
        return services;
    }

    public static void RegisterPavoCommands(AgentCommandHandlerRegistry registry)
    {
        registry.Register<PavoConnectionTestCommand, PavoConnectionTestCommandHandler>("PavoConnectionTest");
    }
}
