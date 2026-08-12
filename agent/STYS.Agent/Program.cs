using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Configuration;
using STYS.Agent.Diagnostics;
using STYS.Agent.LocalDevices;
using STYS.Agent.LocalManagement;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Services;
using STYS.Agent.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();
builder.Host.UseSystemd();

var bootstrapPathResolver = new AgentPathResolver();
var inMemoryLogBuffer = new AgentInMemoryLogBuffer();
builder.Logging.AddProvider(new AgentInMemoryLogProvider(inMemoryLogBuffer));
var bootstrapStoreForStartup = new FileAgentBootstrapConfigurationStore(
    bootstrapPathResolver,
    NullLogger<FileAgentBootstrapConfigurationStore>.Instance);
var startupBootstrap = bootstrapStoreForStartup.TryGetAsync(CancellationToken.None).GetAwaiter().GetResult();

if (startupBootstrap is not null)
{
    var bootstrapSettings = new Dictionary<string, string?>
    {
        ["AgentBootstrap:StysBaseUrl"] = startupBootstrap.StysBaseUrl,
        ["StysAgentClient:BaseUrl"] = startupBootstrap.StysBaseUrl,
        ["AgentBootstrap:HttpTimeoutSeconds"] = startupBootstrap.HttpTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["StysAgentClient:RequestTimeoutSeconds"] = startupBootstrap.HttpTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["AgentBootstrap:LocalUiPort"] = startupBootstrap.LocalUiPort.ToString(CultureInfo.InvariantCulture),
        ["AgentBootstrap:AgentDisplayName"] = startupBootstrap.AgentDisplayName
    };

    builder.Configuration.AddInMemoryCollection(bootstrapSettings);
    var bootstrapSource = builder.Configuration.Sources[^1];
    var environmentSourceIndex = builder.Configuration.Sources
        .Select((source, index) => new { source, index })
        .FirstOrDefault(x => x.source.GetType().Name.Contains("EnvironmentVariables", StringComparison.OrdinalIgnoreCase))?.index;

    if (environmentSourceIndex is not null)
    {
        builder.Configuration.Sources.Remove(bootstrapSource);
        builder.Configuration.Sources.Insert(environmentSourceIndex.Value, bootstrapSource);
    }
}

var localUiPort = startupBootstrap?.LocalUiPort > 0 ? startupBootstrap.LocalUiPort : 5180;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(AgentLocalWebHostBinding.CreateLoopbackEndpoint(localUiPort));
});

builder.Services.Configure<StysAgentClientOptions>(
    builder.Configuration.GetSection(StysAgentClientOptions.SectionName));

builder.Services.AddSingleton<IAgentPathResolver, AgentPathResolver>();
builder.Services.AddSingleton<IAgentBootstrapConfigurationStore, FileAgentBootstrapConfigurationStore>();
builder.Services.AddSingleton<AgentBootstrapConnectionTestState>();
builder.Services.AddSingleton<IAgentBootstrapConnectionTester, AgentBootstrapConnectionTester>();
builder.Services.AddSingleton<IAgentLogBuffer>(inMemoryLogBuffer);
builder.Services.AddSingleton<IAgentRuntimeStatus, AgentRuntimeStatus>();
builder.Services.AddScoped<IAgentBootstrapManagementService, AgentBootstrapManagementService>();
builder.Services.AddSingleton<IAgentEnrollmentCoordinator, AgentEnrollmentCoordinator>();
builder.Services.AddSingleton<ILocalDeviceStore, FileLocalDeviceStore>();
builder.Services.AddSingleton<ILocalDeviceTerminalStore, FileLocalDeviceTerminalStore>();
builder.Services.AddSingleton<IPavoLocalPairingStore, FilePavoLocalPairingStore>();
builder.Services.AddSingleton<IPavoCommandSequenceReservationService, PavoCommandSequenceReservationService>();
builder.Services.AddSingleton<ILocalDeviceConnectionTester, PavoLocalDeviceConnectionTester>();
builder.Services.AddSingleton<ILocalDeviceConnectionTesterRegistry, LocalDeviceConnectionTesterRegistry>();
builder.Services.AddScoped<ILocalDeviceManagementService, LocalDeviceManagementService>();

builder.Services.AddSingleton<AgentTokenStore>();
builder.Services.AddSingleton<IAgentAuthenticationState, AgentAuthenticationState>();
builder.Services.AddSingleton<IAgentCredentialStore, FileAgentCredentialStore>();
builder.Services.AddTransient<AgentAuthenticationHandler>();
builder.Services.AddSingleton<IPavoClient, PavoHttpClient>();

builder.Services.AddHttpClient<IStysAgentApiClient, StysAgentApiClient>((sp, client) =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
    .AddHttpMessageHandler<AgentAuthenticationHandler>();
builder.Services.AddHttpClient(nameof(AgentBootstrapConnectionTester));

builder.Services.AddHostedService<AgentHostedService>();
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<CommandPollingWorker>();

builder.Services.AddSingleton<IAgentCommandExecutionStore, FileAgentCommandExecutionStore>();
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

var app = builder.Build();
app.MapAgentLocalManagement();

await app.RunAsync();
