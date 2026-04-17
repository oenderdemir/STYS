using Microsoft.Extensions.Logging;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Tüm lisans doğrulama adımlarını orkestra eder.
/// Her adım bağımsız bir hata mesajı üretir, böylece tanılama kolaylaşır.
/// </summary>
public sealed class LicenseValidator : ILicenseValidator
{
    private readonly ILicenseReader _reader;
    private readonly ILicenseSignatureVerifier _signatureVerifier;
    private readonly IRuntimeFingerprintProvider _fingerprintProvider;
    private readonly ITimeRollbackGuard _timeRollbackGuard;
    private readonly IAssemblyIntegrityChecker _integrityChecker;
    private readonly ILogger<LicenseValidator> _logger;

    public LicenseValidator(
        ILicenseReader reader,
        ILicenseSignatureVerifier signatureVerifier,
        IRuntimeFingerprintProvider fingerprintProvider,
        ITimeRollbackGuard timeRollbackGuard,
        IAssemblyIntegrityChecker integrityChecker,
        ILogger<LicenseValidator> logger)
    {
        _reader = reader;
        _signatureVerifier = signatureVerifier;
        _fingerprintProvider = fingerprintProvider;
        _timeRollbackGuard = timeRollbackGuard;
        _integrityChecker = integrityChecker;
        _logger = logger;
    }

    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // 1. Assembly bütünlük kontrolü
        if (!_integrityChecker.IsIntact())
        {
            _logger.LogCritical("Lisanslama assembly bütünlüğü bozulmuş.");
            errors.Add("Assembly bütünlük kontrolü başarısız.");
        }

        // 2. Zaman geri alma kontrolü
        if (_timeRollbackGuard.IsTimeRolledBack())
        {
            _logger.LogCritical("Sistem saatinin geri alındığı tespit edildi.");
            errors.Add("Sistem saati geri alınmış. Lisans doğrulanamıyor.");
        }

        // Yukarıdaki kritik hatalar varsa lisans okumaya bile gerek yok
        if (errors.Count > 0)
            return LicenseValidationResult.Failure(errors);

        // 3. Lisans dosyasını oku
        LicenseDocument license;
        try
        {
            license = await _reader.ReadAsync(cancellationToken);
        }
        catch (LicenseException ex)
        {
            _logger.LogError(ex, "Lisans dosyası okunamadı.");
            return LicenseValidationResult.Failure(ex.Message);
        }

        // 4. İmza doğrulama
        if (!_signatureVerifier.Verify(license))
        {
            _logger.LogWarning("Lisans imzası geçersiz. LicenseId: {LicenseId}", license.LicenseId);
            errors.Add("Lisans imzası geçersiz. Lisans dosyası değiştirilmiş olabilir.");
        }

        // 5. Süre kontrolü
        var now = DateTimeOffset.UtcNow;
        if (now > license.ExpiresAtUtc)
        {
            _logger.LogWarning("Lisans süresi dolmuş. ExpiresAtUtc: {ExpiresAt}, Now: {Now}",
                license.ExpiresAtUtc, now);
            errors.Add($"Lisans süresi dolmuş. Son kullanma: {license.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC");
        }

        if (now < license.IssuedAtUtc)
        {
            _logger.LogWarning("Lisans henüz geçerli değil. IssuedAtUtc: {IssuedAt}", license.IssuedAtUtc);
            errors.Add("Lisans henüz geçerli değil.");
        }

        // 6. Fingerprint kontrolü
        var currentFingerprint = _fingerprintProvider.ComputeFingerprint();
        if (!string.Equals(license.FingerprintHash, currentFingerprint, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Ortam parmak izi uyuşmuyor. Lisans başka bir ortam için üretilmiş. " +
                "Beklenen: {Expected}, Mevcut: {Actual}",
                license.FingerprintHash, currentFingerprint);
            errors.Add("Ortam parmak izi uyuşmuyor. Bu lisans bu ortam için geçerli değil.");
        }

        if (errors.Count > 0)
            return LicenseValidationResult.Failure(errors);

        // Tüm kontrollerden geçti — zamanı kaydet
        _timeRollbackGuard.RecordCurrentTime();

        _logger.LogInformation(
            "Lisans doğrulandı. LicenseId: {LicenseId}, Customer: {Customer}, Expires: {Expires}",
            license.LicenseId, license.CustomerCode, license.ExpiresAtUtc);

        return LicenseValidationResult.Success(license);
    }
}
