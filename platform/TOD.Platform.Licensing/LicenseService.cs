using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Lisans durumunu cache'leyerek sunar.
/// Cache süresi dolduğunda arka planda yeniden doğrulama yapar.
/// Middleware, filter ve servis katmanı bu sınıfı kullanır.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    private readonly ILicenseValidator _validator;
    private readonly ILogger<LicenseService> _logger;
    private readonly TimeSpan _cacheDuration;

    private LicenseValidationResult? _cachedResult;
    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LicenseService(
        ILicenseValidator validator,
        IOptions<LicensingOptions> options,
        ILogger<LicenseService> logger)
    {
        _validator = validator;
        _logger = logger;
        _cacheDuration = TimeSpan.FromSeconds(options.Value.CacheDurationSeconds);
    }

    public async Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default)
    {
        // Cache geçerliyse hemen dön
        if (_cachedResult is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
            return _cachedResult;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern
            if (_cachedResult is not null && DateTimeOffset.UtcNow < _cacheExpiresAt)
                return _cachedResult;

            _logger.LogDebug("Lisans yeniden doğrulanıyor...");

            var result = await _validator.ValidateAsync(cancellationToken);
            _cachedResult = result;
            _cacheExpiresAt = DateTimeOffset.UtcNow + _cacheDuration;

            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default)
    {
        var status = await GetCurrentStatusAsync(cancellationToken);

        if (!status.IsValid || status.License is null)
            return false;

        // EnabledModules boşsa tüm modüller aktif (tam lisans)
        if (status.License.EnabledModules.Count == 0)
            return true;

        return status.License.EnabledModules
            .Any(m => string.Equals(m, moduleCode, StringComparison.OrdinalIgnoreCase));
    }

    public void InvalidateCache()
    {
        _cachedResult = null;
        _cacheExpiresAt = DateTimeOffset.MinValue;
        _logger.LogInformation("Lisans cache temizlendi.");
    }
}
