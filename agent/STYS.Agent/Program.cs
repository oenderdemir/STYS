using STYS.Agent.Client;
using STYS.Agent.Client.Authentication;
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

builder.Services.AddHttpClient<IStysAgentApiClient, StysAgentApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<StysAgentClientOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

builder.Services.AddSingleton<AgentTokenStore>();
builder.Services.AddSingleton<AgentHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentHostedService>());
builder.Services.AddHostedService<HeartbeatWorker>();
builder.Services.AddHostedService<CommandPollingWorker>();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/agent-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var host = builder.Build();
await host.RunAsync();
