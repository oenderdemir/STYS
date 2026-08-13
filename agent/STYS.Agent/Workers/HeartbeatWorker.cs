using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Contracts.Versioning;
using STYS.Agent.Services;
using STYS.Agent.Versioning;

namespace STYS.Agent.Workers;

public sealed class HeartbeatWorker : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly IAgentAuthenticationState _authenticationState;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly ILogger<HeartbeatWorker> _logger;
    public HeartbeatWorker(
        IStysAgentApiClient client,
        IAgentAuthenticationState authenticationState,
        IAgentRuntimeStatus runtimeStatus,
        ILogger<HeartbeatWorker> logger)
    {
        _client = client;
        _authenticationState = authenticationState;
        _runtimeStatus = runtimeStatus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _authenticationState.WaitUntilReadyAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested && _authenticationState.IsReady)
            {
                try
                {
                    var request = new AgentHeartbeatRequest
                    {
                        AgentVersion = AgentVersionInfo.Current,
                        ContractVersion = AgentContractVersion.Current,
                        RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier,
                        SupportedApiVersions = ["v1"],
                        SupportedCapabilities = ["heartbeat", "config-read"],
                        InstalledModules =
                        [
                            new AgentModuleInfo { ModuleName = "Core", ModuleVersion = AgentVersionInfo.Current }
                        ],
                        Platform = RuntimeInformation.OSDescription,
                        OsVersion = Environment.OSVersion.ToString()
                    };

                    await _client.SendHeartbeatAsync(request, stoppingToken);
                    _runtimeStatus.MarkHeartbeatSuccess();
                    _logger.LogDebug("Heartbeat gönderildi.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _runtimeStatus.MarkHeartbeatFailure(ex.Message);
                    _logger.LogWarning(ex, "Heartbeat gönderilemedi.");
                }

                if (!await DelayWhileAuthenticatedAsync(TimeSpan.FromSeconds(30), stoppingToken))
                    break;
            }
        }
    }

    private async Task<bool> DelayWhileAuthenticatedAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero && !cancellationToken.IsCancellationRequested && _authenticationState.IsReady)
        {
            var slice = remaining > TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : remaining;
            await Task.Delay(slice, cancellationToken);
            remaining -= slice;
        }

        return _authenticationState.IsReady && !cancellationToken.IsCancellationRequested;
    }
}
