using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Services;
using STYS.Agent.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();
builder.Services.AddSystemd();

builder.Services.Configure<StysAgentClientOptions>(
    builder.Configuration.GetSection(StysAgentClientOptions.SectionName));

builder.Services.AddSingleton<AgentTokenStore>();
builder.Services.AddSingleton<IAgentCredentialStore, FileAgentCredentialStore>();
builder.Services.AddTransient<AgentAuthenticationHandler>();

builder.Services.AddHttpClient<IStysAgentApiClient, StysAgentApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StysAgentClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
})
.AddHttpMessageHandler<AgentAuthenticationHandler>();

builder.Services.AddSingleton<AgentHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentHostedService>());
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<CommandPollingWorker>();

builder.Services.AddSingleton<IAgentCommandExecutionStore, MemoryAgentCommandExecutionStore>();
builder.Services.AddSingleton<IAgentCommandHandlerRegistry>(sp =>
{
    var registry = new AgentCommandHandlerRegistry(sp);
    registry.Register<PingCommand, PingCommandHandler>("Ping");
    registry.Register<HealthCheckCommand, HealthCheckCommandHandler>("HealthCheck");
    registry.Register<RefreshConfigurationCommand, RefreshConfigCommandHandler>("RefreshConfiguration");
    PavoModuleExtensions.RegisterPavoCommands(registry);
    return registry;
});

builder.Services.AddScoped<PingCommandHandler>();
builder.Services.AddScoped<HealthCheckCommandHandler>();
builder.Services.AddScoped<RefreshConfigCommandHandler>();
builder.Services.AddPavoModule();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/agent-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var host = builder.Build();
await host.RunAsync();
