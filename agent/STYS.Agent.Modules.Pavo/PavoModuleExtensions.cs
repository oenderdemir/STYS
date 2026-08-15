using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Commands;
using STYS.Agent.Modules.Pavo.Commands;

namespace STYS.Agent.Modules.Pavo;

public static class PavoModuleExtensions
{
    public static IServiceCollection AddPavoModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PavoAgentOptions>(configuration.GetSection(PavoAgentOptions.SectionName));
        services.PostConfigure<PavoAgentOptions>(options =>
        {
            options.Fingerprint = PavoAgentOptions.ResolveFingerprint(
                options.Fingerprint,
                Environment.GetEnvironmentVariable(PavoAgentOptions.FingerprintEnvironmentVariable));
            options.TimeoutSeconds = PavoAgentOptions.ResolveTimeoutSeconds(options.TimeoutSeconds);
        });

        services.AddScoped<PavoPairingCommandHandler>();
        services.AddScoped<PavoPingCommandHandler>();
        services.AddScoped<PavoGetDeviceInfoCommandHandler>();
        services.AddScoped<PavoStartPaymentCommandHandler>();
        services.AddScoped<PavoGetPaymentResultCommandHandler>();
        services.AddHttpClient("PavoClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PavoAgentOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(PavoAgentOptions.ResolveTimeoutSeconds(options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
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
