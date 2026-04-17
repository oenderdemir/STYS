namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisansın tüm doğrulama adımlarını orkestra eder:
/// imza, süre, fingerprint, zaman geri alma, bütünlük.
/// </summary>
public interface ILicenseValidator
{
    Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
}
