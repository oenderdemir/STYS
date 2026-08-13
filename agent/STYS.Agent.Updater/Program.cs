using Serilog;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Options;
using STYS.Agent.Updater.Services;
using STYS.Agent.Updater.Options;

var bootstrapPathResolver = new AgentPathResolver();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(bootstrapPathResolver.LogDirectory, "updater-.log"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .UseSystemd()
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<IAgentPathResolver, AgentPathResolver>();
            services.AddSingleton<IAgentUpgradeRequestStore, FileAgentUpgradeRequestStore>();
            services.AddSingleton<IAgentUpgradeOutcomeStore, FileAgentUpgradeOutcomeStore>();
            services.AddSingleton<IAgentServiceController, AgentServiceController>();
            services.AddHttpClient<IAgentHealthProbe, AgentHealthProbe>();
            services.AddHostedService<AgentUpgradeMonitorWorker>();
            services.Configure<AgentUpgradeOptions>(context.Configuration.GetSection(AgentUpgradeOptions.SectionName));
            services.AddSingleton(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                return new AgentUpgradeRuntimeOptions
                {
                    InstallDirectory = NormalizePath(configuration["STYS_AGENT_INSTALL_DIR"]) ?? DefaultInstallDirectory(),
                    ServiceName = string.IsNullOrWhiteSpace(configuration["STYS_AGENT_SERVICE_NAME"]) ? DefaultServiceName() : configuration["STYS_AGENT_SERVICE_NAME"]!,
                    LocalUiPort = ParsePort(configuration["STYS_AGENT_LOCAL_UI_PORT"], 5180),
                    PollIntervalSeconds = ParseInt(configuration["STYS_AGENT_UPGRADE_POLL_SECONDS"], 5),
                    HealthTimeoutSeconds = ParseInt(configuration["STYS_AGENT_UPGRADE_HEALTH_TIMEOUT_SECONDS"], 90)
                };
            });
        });

    await builder.Build().RunAsync();
}
finally
{
    Log.CloseAndFlush();
}

static string DefaultInstallDirectory() =>
    OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "STYS", "Agent")
        : "/opt/stys-agent";

static string DefaultServiceName() => OperatingSystem.IsWindows() ? "STYS Agent" : "stys-agent";

static int ParsePort(string? value, int fallback)
{
    return int.TryParse(value, out var port) && port is > 0 and <= 65535 ? port : fallback;
}

static int ParseInt(string? value, int fallback) =>
    int.TryParse(value, out var result) && result > 0 ? result : fallback;

static string? NormalizePath(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    return Path.GetFullPath(path.Trim());
}
