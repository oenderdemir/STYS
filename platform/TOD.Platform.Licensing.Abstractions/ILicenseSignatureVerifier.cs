namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans belgesinin dijital imzasını doğrular.
/// Uygulama tarafında yalnızca public key ile çalışır.
/// </summary>
public interface ILicenseSignatureVerifier
{
    bool Verify(LicenseDocument license);
}
