using TOD.Platform.Licensing.Abstractions;

namespace STYS.Licensing.Services;

public sealed class LicenseAwareMaintenanceHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LicenseAwareMaintenanceHostedService> _logger;

    public LicenseAwareMaintenanceHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<LicenseAwareMaintenanceHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseService>();

                // Middleware disi akislar icin de lisansi zorla.
                await licenseService.EnsureLicensedAsync(stoppingToken);
                await licenseService.EnsureModuleLicensedAsync(StysLicensedModules.Bildirim, stoppingToken);
            }
            catch (LicenseException ex)
            {
                _logger.LogWarning(
                    ex,
                    "LicenseAwareMaintenanceHostedService: lisans/modul dogrulamasi basarisiz. " +
                    "Worker isi skip edildi, bir sonraki periyotta tekrar denenecek.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LicenseAwareMaintenanceHostedService beklenmeyen hata.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
