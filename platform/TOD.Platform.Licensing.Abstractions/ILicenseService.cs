namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans durumunu cache'leyerek sunar.
/// Middleware ve servis katmanı bu arayüzü kullanır.
/// </summary>
public interface ILicenseService
{
    /// <summary>Cache'li lisans durumunu döner. Periyodik olarak yeniden doğrular.</summary>
    Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Belirli bir modülün lisanslı olup olmadığını kontrol eder.</summary>
    Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default);

    /// <summary>Cache'i zorla temizler ve yeniden doğrulama yapılmasını sağlar.</summary>
    void InvalidateCache();
}
