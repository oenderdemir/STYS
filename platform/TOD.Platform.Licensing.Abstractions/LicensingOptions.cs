namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisanslama altyapısının yapılandırma seçenekleri.
/// appsettings.json üzerinden bind edilir.
/// </summary>
public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    /// <summary>Lisans dosyasının yolu. Varsayılan: "license.json"</summary>
    public string LicenseFilePath { get; set; } = "license.json";

    /// <summary>Ortam adı. Lisanstaki EnvironmentName ile eşleşmelidir.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Instance ID. Her deployment instance'ı için benzersiz olmalıdır.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Müşteri kodu. Fingerprint hesaplamasında kullanılır.</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>Deployment marker. Container/K8s ortamlarında ek bağlama sağlar.</summary>
    public string DeploymentMarker { get; set; } = string.Empty;

    /// <summary>
    /// Cache süresi (saniye). Bu süre boyunca lisans tekrar doğrulanmaz.
    /// Varsayılan: 300 (5 dakika).
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Zaman geri alma koruması için state dosyasının yolu.
    /// Varsayılan: ".license-state"
    /// </summary>
    public string TimeGuardStatePath { get; set; } = ".license-state";

    /// <summary>
    /// Lisans kontrolünden muaf tutulacak path prefix'leri.
    /// Örneğin: ["/health", "/license-status"]
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = [];
}
