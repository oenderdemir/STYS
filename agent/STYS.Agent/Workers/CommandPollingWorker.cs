using STYS.Agent.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace STYS.Agent.Workers;

public sealed class CommandPollingWorker : BackgroundService
{
    private readonly IStysAgentApiClient _client;
    private readonly ILogger<CommandPollingWorker> _logger;

    public CommandPollingWorker(IStysAgentApiClient client, ILogger<CommandPollingWorker> logger)
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
                var commands = await _client.GetPendingCommandsAsync(stoppingToken);
                foreach (var command in commands)
                {
                    _logger.LogInformation("Komut alındı: {CommandType} ({CommandId})", command.CommandType, command.CommandId);
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotImplemented)
            {
                _logger.LogDebug("Komut endpoint'i henüz implemente edilmedi.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Komut kontrolü başarısız.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
