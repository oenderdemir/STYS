using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Calisma ortaminin parmak izini profile-based olarak uretir.
///
/// Profile secimi:
/// - <see cref="FingerprintProfile.PhysicalServer"/>: MachineName + OS dahil tam fingerprint.
///   Fiziksel sunucu veya sabit VM icin uygundur.
/// - <see cref="FingerprintProfile.Container"/>: MachineName ve OS disarida birakilir.
///   Container/K8s ortamlarinda hostname ve OS restart'ta degisebilir; bu profilde
///   <see cref="LicensingOptions.DeploymentMarker"/> zorunludur (K8s deployment adi,
///   Docker image hash vb.).
///
/// Fingerprint bilesenleri (ortak):
/// - EnvironmentName
/// - InstanceId
/// - CustomerCode
/// - DeploymentMarker
///
/// PhysicalServer profilinde ek olarak:
/// - MachineName
/// - OS Description
/// </summary>
public sealed class RuntimeFingerprintProvider : IRuntimeFingerprintProvider
{
    private readonly LicensingOptions _options;

    public RuntimeFingerprintProvider(IOptions<LicensingOptions> options)
    {
        _options = options.Value;

        if (_options.FingerprintProfile == FingerprintProfile.Container
            && string.IsNullOrWhiteSpace(_options.DeploymentMarker))
        {
            throw new LicenseException(
                "Container fingerprint profili secildi ancak DeploymentMarker bos. " +
                "Container/K8s ortamlarinda sabit bir DeploymentMarker degeri gereklidir.");
        }
    }

    public string ComputeFingerprint()
    {
        var builder = new StringBuilder();

        // Ortak bilesenler (deterministik sira)
        builder.Append("PROFILE:");
        builder.Append(_options.FingerprintProfile.ToString().ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.EnvironmentName.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.InstanceId.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.CustomerCode.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.DeploymentMarker.ToUpperInvariant());

        // PhysicalServer profilinde makine kimligi fingerprint'e dahil edilir.
        // Container profilinde host degisken oldugu icin bu bilesenler atlanir.
        if (_options.FingerprintProfile == FingerprintProfile.PhysicalServer)
        {
            builder.Append('|');
            builder.Append(Environment.MachineName.ToUpperInvariant());
            builder.Append('|');
            builder.Append(RuntimeInformation.OSDescription.ToUpperInvariant());
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToBase64String(bytes);
    }
}
