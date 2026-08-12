namespace STYS.Agent.Services;

public sealed class AgentCommandExpiryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentCommandExpiryHostedService> _logger;

    public AgentCommandExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgentCommandExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(30);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AgentCommandExpiryService>();
                var expiredCount = await service.ExpireTimedOutCommandsAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Agent command expiry worker completed. ExpiredCount={ExpiredCount}", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent command expiry worker failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
