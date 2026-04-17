namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Çalışma ortamının parmak izini üretir.
/// Lisans dosyasındaki fingerprintHash ile karşılaştırılır.
/// </summary>
public interface IRuntimeFingerprintProvider
{
    string ComputeFingerprint();
}
