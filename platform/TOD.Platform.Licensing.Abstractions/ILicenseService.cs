namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans durumunu cache'leyerek sunar.
/// Middleware, filter ve servis katmani bu arayuzu kullanir.
/// </summary>
public interface ILicenseService
{
    /// <summary>Cache'li lisans durumunu doner. Periyodik olarak yeniden dogrular.</summary>
    Task<LicenseValidationResult> GetCurrentStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Belirli bir modulun lisansli olup olmadigini kontrol eder.</summary>
    Task<bool> IsModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default);

    /// <summary>Cache'i zorla temizler ve yeniden dogrulama yapilmasini saglar.</summary>
    void InvalidateCache();

    /// <summary>
    /// Lisans gecerli degilse <see cref="LicenseException"/> firlatir.
    /// Servis/hub/background job gibi HTTP pipeline disi kritik noktalarda kullanilir.
    /// </summary>
    Task EnsureLicensedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen modul lisansli degilse <see cref="LicenseException"/> firlatir.
    /// Modul bazli erisim kontrolunun zorlanmasi gereken noktalarda kullanilir
    /// (ornegin rapor/export/write operasyonlari).
    /// </summary>
    Task EnsureModuleLicensedAsync(string moduleCode, CancellationToken cancellationToken = default);
}
