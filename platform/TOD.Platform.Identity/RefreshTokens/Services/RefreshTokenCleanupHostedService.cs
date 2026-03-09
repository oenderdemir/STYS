using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Options;

namespace TOD.Platform.Identity.RefreshTokens.Services;

public class RefreshTokenCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptions<JwtTokenOptions> _jwtTokenOptions;
    private readonly ILogger<RefreshTokenCleanupHostedService> _logger;

    public RefreshTokenCleanupHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<JwtTokenOptions> jwtTokenOptions,
        ILogger<RefreshTokenCleanupHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _jwtTokenOptions = jwtTokenOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupDelay = TimeSpan.FromMinutes(Math.Max(0, _jwtTokenOptions.Value.RefreshTokenCleanupStartupDelayMinutes));
        if (startupDelay > TimeSpan.Zero)
        {
            await Task.Delay(startupDelay, stoppingToken);
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _jwtTokenOptions.Value.RefreshTokenCleanupIntervalHours));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Refresh token cleanup job failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }

    private async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken)
    {
        var retentionDays = Math.Max(0, _jwtTokenOptions.Value.RefreshTokenRetentionDays);
        var retentionCutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);

        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TodIdentityDbContext>();

        var deletedCount = await dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .Where(x =>
                x.ExpiresAt <= retentionCutoffUtc
                || (x.RevokedAt.HasValue && x.RevokedAt.Value <= retentionCutoffUtc))
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation(
                "Refresh token cleanup removed {DeletedCount} records (retentionDays={RetentionDays}).",
                deletedCount,
                retentionDays);
        }
    }
}
