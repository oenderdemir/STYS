namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans dosyasını diskten (veya başka bir kaynaktan) okuyup deserialize eder.
/// </summary>
public interface ILicenseReader
{
    Task<LicenseDocument> ReadAsync(CancellationToken cancellationToken = default);
}
