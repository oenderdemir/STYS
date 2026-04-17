using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Çalışma ortamının parmak izini üretir.
///
/// Fingerprint bileşenleri:
/// - EnvironmentName: Ortam adı (Production, Staging vb.)
/// - InstanceId: Deployment instance kimliği
/// - MachineName: Sunucu adı
/// - OS Description: İşletim sistemi bilgisi
/// - CustomerCode: Müşteri kodu
/// - DeploymentMarker: Opsiyonel ek bağlama (K8s pod-name-prefix, Docker image hash vb.)
///
/// Container/Kubernetes notları:
/// - MachineName container'da hostname olur (pod adı). Rastgele değişir.
///   Bu yüzden K8s ortamlarında DeploymentMarker ile sabit bir değer verilmeli.
/// - OS Description container'da host OS'u değil container OS'u döner.
///   Bu tutarlılık sağlar çünkü aynı image her yerde aynı OS bilgisi verir.
/// - InstanceId olarak K8s deployment adı veya StatefulSet adı kullanılabilir.
/// - VM'lerde MachineName sabit olduğu için ek bağlama gerekmez.
/// </summary>
public sealed class RuntimeFingerprintProvider : IRuntimeFingerprintProvider
{
    private readonly LicensingOptions _options;

    public RuntimeFingerprintProvider(IOptions<LicensingOptions> options)
    {
        _options = options.Value;
    }

    public string ComputeFingerprint()
    {
        var builder = new StringBuilder();

        // Her bileşen pipe ile ayrılır, deterministik sıra korunur.
        builder.Append(_options.EnvironmentName.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.InstanceId.ToUpperInvariant());
        builder.Append('|');
        builder.Append(Environment.MachineName.ToUpperInvariant());
        builder.Append('|');
        builder.Append(RuntimeInformation.OSDescription.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.CustomerCode.ToUpperInvariant());
        builder.Append('|');
        builder.Append(_options.DeploymentMarker.ToUpperInvariant());

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToBase64String(bytes);
    }
}
