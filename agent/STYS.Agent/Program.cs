using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Configuration;
using STYS.Agent.Diagnostics;
using STYS.Agent.LocalDevices;
using STYS.Agent.LocalManagement;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Options;
using STYS.Agent.Services;
using STYS.Agent.Upgrade;
using STYS.Agent.Workers;
using STYS.Agent.Versioning;

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
var localUiPort = ResolveLocalUiPort(startupBootstrap?.LocalUiPort);

if (startupBootstrap is not null)
{
    var bootstrapSettings = new Dictionary<string, string?>
    {
        ["AgentBootstrap:StysBaseUrl"] = startupBootstrap.StysBaseUrl,
        ["StysAgentClient:BaseUrl"] = startupBootstrap.StysBaseUrl,
        ["AgentBootstrap:HttpTimeoutSeconds"] = startupBootstrap.HttpTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["StysAgentClient:RequestTimeoutSeconds"] = startupBootstrap.HttpTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["AgentBootstrap:LocalUiPort"] = localUiPort.ToString(CultureInfo.InvariantCulture),
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

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(AgentLocalWebHostBinding.CreateLoopbackEndpoint(localUiPort));
});

builder.Services.Configure<StysAgentClientOptions>(
    builder.Configuration.GetSection(StysAgentClientOptions.SectionName));
builder.Services.PostConfigure<StysAgentClientOptions>(options => options.AgentVersion = AgentVersionInfo.Current);
builder.Services.Configure<AgentUpgradeOptions>(builder.Configuration.GetSection(AgentUpgradeOptions.SectionName));

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
builder.Services.AddSingleton<IAgentReleaseStagingStore, FileAgentReleaseStagingStore>();
builder.Services.AddScoped<IAgentReleaseStagingService, AgentReleaseStagingService>();

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
builder.Services.AddHostedService<AgentUpgradeOutcomeReporterWorker>();

builder.Services.AddAgentProductionInfrastructure();
builder.Services.AddSingleton<IAgentCommandHandlerRegistry>(sp =>
{
    var registry = new AgentCommandHandlerRegistry();
    registry.Register<PingCommand, PingCommandHandler>("Ping");
    registry.Register<HealthCheckCommand, HealthCheckCommandHandler>("HealthCheck");
    registry.Register<RefreshConfigurationCommand, RefreshConfigCommandHandler>("RefreshConfiguration");
    registry.Register<AgentStageUpgradeCommand, AgentStageUpgradeCommandHandler>("AgentStageUpgrade");
    registry.Register<AgentApplyUpgradeCommand, AgentApplyUpgradeCommandHandler>("AgentApplyUpgrade");
    PavoModuleExtensions.RegisterPavoCommands(registry);
    return registry;
});

builder.Services.AddScoped<PingCommandHandler>();
builder.Services.AddScoped<HealthCheckCommandHandler>();
builder.Services.AddScoped<RefreshConfigCommandHandler>();
builder.Services.AddScoped<AgentStageUpgradeCommandHandler>();
builder.Services.AddScoped<AgentApplyUpgradeCommandHandler>();
builder.Services.AddPavoModule();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(bootstrapPathResolver.LogDirectory, "agent-.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var app = builder.Build();
app.MapAgentLocalManagement();

using (var scope = app.Services.CreateScope())
{
    var startupValidator = scope.ServiceProvider.GetRequiredService<IAgentStartupValidationService>();
    await startupValidator.ValidateAsync(CancellationToken.None);
}

await app.RunAsync();

static int ResolveLocalUiPort(int? configuredPort)
{
    var environmentValue = Environment.GetEnvironmentVariable("STYS_AGENT_LOCAL_UI_PORT");
    if (int.TryParse(environmentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var environmentPort) && environmentPort > 0 && environmentPort <= 65535)
    {
        return environmentPort;
    }

    if (configuredPort is > 0 and <= 65535)
    {
        return configuredPort.Value;
    }

    return 5180;
}
