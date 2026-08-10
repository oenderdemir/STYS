using Microsoft.Extensions.DependencyInjection;
using STYS.Agent.Client.Commands;
using STYS.Agent.Modules.Pavo.Commands;

namespace STYS.Agent.Modules.Pavo;

public static class PavoModuleExtensions
{
    public static IServiceCollection AddPavoModule(this IServiceCollection services)
    {
        services.AddScoped<PavoPairingCommandHandler>();
        services.AddScoped<PavoPingCommandHandler>();
        services.AddScoped<PavoGetDeviceInfoCommandHandler>();
        services.AddScoped<PavoStartPaymentCommandHandler>();
        services.AddScoped<PavoGetPaymentResultCommandHandler>();
        services.AddHttpClient("PavoClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IPavoRestClient, PavoRestClient>();
        return services;
    }

    public static void RegisterPavoCommands(AgentCommandHandlerRegistry registry)
    {
        registry.Register<PavoPairingCommand, PavoPairingCommandHandler>("PavoPairing");
        registry.Register<PavoPingCommand, PavoPingCommandHandler>("PavoPing");
        registry.Register<PavoGetDeviceInfoCommand, PavoGetDeviceInfoCommandHandler>("PavoGetDeviceInfo");
        registry.Register<PavoStartPaymentCommand, PavoStartPaymentCommandHandler>("PavoStartPayment");
        registry.Register<PavoGetPaymentResultCommand, PavoGetPaymentResultCommandHandler>("PavoGetPaymentResult");
    }
}
