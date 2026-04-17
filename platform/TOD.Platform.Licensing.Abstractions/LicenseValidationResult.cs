namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans doğrulama sonucunu temsil eder.
/// Başarılı/başarısız durumunu ve detaylı hata bilgilerini içerir.
/// </summary>
public sealed class LicenseValidationResult
{
    public bool IsValid { get; private init; }
    public LicenseDocument? License { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static LicenseValidationResult Success(LicenseDocument license) => new()
    {
        IsValid = true,
        License = license,
        Errors = []
    };

    public static LicenseValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        License = null,
        Errors = errors
    };

    public static LicenseValidationResult Failure(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        License = null,
        Errors = errors.ToList().AsReadOnly()
    };
}
