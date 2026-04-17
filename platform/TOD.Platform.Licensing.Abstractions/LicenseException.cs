namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans doğrulama hatalarında fırlatılan özel exception.
/// Startup veya middleware seviyesinde yakalanarak uygun HTTP yanıtına çevrilir.
/// </summary>
public class LicenseException : Exception
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public LicenseException(string message)
        : base(message)
    {
        ValidationErrors = [message];
    }

    public LicenseException(string message, IReadOnlyList<string> errors)
        : base(message)
    {
        ValidationErrors = errors;
    }

    public LicenseException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationErrors = [message];
    }
}
