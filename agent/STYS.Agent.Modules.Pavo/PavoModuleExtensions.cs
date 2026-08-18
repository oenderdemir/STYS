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
        services.AddScoped<PavoPerformEodCommandHandler>();
        services.AddHttpClient("PavoClient", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<PavoAgentOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(PavoAgentOptions.ResolveTimeoutSeconds(options.TimeoutSeconds));
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PavoAgentOptions>>().Value;

            // A POS terminal sits on the local network: the TCP handshake either completes quickly
            // or the device is not there. The overall Timeout stays long because a card transaction
            // legitimately takes time once the request is in flight, but waiting that long merely to
            // discover an unplugged device is wasted. Bounding only the connect phase gives a fast
            // answer without shortening any request the device actually received.
            return new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(PavoAgentOptions.ResolveConnectTimeoutSeconds(options.ConnectTimeoutSeconds))
            };
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
        registry.Register<PavoPerformEodCommand, PavoPerformEodCommandHandler>("PavoPerformEOD");
    }
}
