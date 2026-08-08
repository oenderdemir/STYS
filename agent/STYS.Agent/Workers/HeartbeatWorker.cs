using STYS.Agent.Client;
using STYS.Agent.Contracts.Dtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace STYS.Agent.Workers;

public sealed class HeartbeatWorker : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly string _agentVersion = "1.0.0";
    private readonly string _contractVersion = "1.0.0";

    public HeartbeatWorker(IStysAgentApiClient client, ILogger<HeartbeatWorker> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new AgentHeartbeatRequest
                {
                    AgentVersion = _agentVersion,
                    ContractVersion = _contractVersion,
                    SupportedApiVersions = ["v1"],
                    SupportedCapabilities = ["heartbeat", "config-read"],
                    InstalledModules =
                    [
                        new AgentModuleInfo { ModuleName = "Core", ModuleVersion = _agentVersion }
                    ],
                    Platform = RuntimeInformation.OSDescription,
                    OsVersion = Environment.OSVersion.ToString()
                };

                await _client.SendHeartbeatAsync(request, stoppingToken);
                _logger.LogDebug("Heartbeat gönderildi.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Heartbeat gönderilemedi.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
