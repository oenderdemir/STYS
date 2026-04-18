namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Fingerprint hesaplama profili. Ortamin dogasi gereken stabilite icin belirler.
/// </summary>
public enum FingerprintProfile
{
    /// <summary>
    /// Fiziksel sunucu / VM. MachineName ve OS bilgisi stabildir, tam fingerprint kullanilir.
    /// </summary>
    PhysicalServer = 0,

    /// <summary>
    /// Container / K8s. MachineName (pod adi) ve OS her restart'ta degisebilir.
    /// Bu profilde MachineName ve OS fingerprint'e dahil edilmez; DeploymentMarker gereklidir.
    /// </summary>
    Container = 1
}

/// <summary>
/// Lisanslama altyapisinin yapilandirma secenekleri.
/// appsettings.json uzerinden bind edilir.
/// </summary>
public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    /// <summary>Lisans dosyasinin yolu. Varsayilan: "license.json"</summary>
    public string LicenseFilePath { get; set; } = "license.json";

    /// <summary>Ortam adi. Lisanstaki EnvironmentName ile eslesmelidir.</summary>
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Instance ID. Her deployment instance'i icin benzersiz olmalidir.</summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Musteri kodu. Fingerprint hesaplamasinda kullanilir.</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>Deployment marker. Container/K8s ortamlarinda ek baglama saglar.</summary>
    public string DeploymentMarker { get; set; } = string.Empty;

    /// <summary>
    /// Fingerprint profili. PhysicalServer (default) tam fingerprint uretir;
    /// Container profilinde MachineName/OS dahil edilmez, DeploymentMarker zorunlu olur.
    /// </summary>
    public FingerprintProfile FingerprintProfile { get; set; } = FingerprintProfile.PhysicalServer;

    /// <summary>
    /// Cache suresi (saniye). Bu sure boyunca lisans tekrar dogrulanmaz.
    /// Varsayilan: 300 (5 dakika).
    /// </summary>
    public int CacheDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Zaman geri alma korumasi icin birincil state dosyasinin yolu.
    /// Varsayilan: ".license-state"
    /// </summary>
    public string TimeGuardStatePath { get; set; } = ".license-state";

    /// <summary>
    /// Opsiyonel ikinci state konumu (directory). Bos birakilirsa otomatik olarak
    /// TimeGuardStatePath ile ayni dizinde gizli bir ".license-state.mirror" kullanilir.
    /// Saat geri alma tespitinde iki kaynak birbirini dogrular.
    /// </summary>
    public string TimeGuardMirrorPath { get; set; } = string.Empty;

    /// <summary>
    /// Lisans kontrolunden muaf tutulacak path prefix'leri.
    /// Guvenlik icin varsayilan eslesme exact'tir.
    /// Prefix eslesme icin "/*" kullanin (ornek: "/auth/*", "/health/*", "/ui/license/*").
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = [];

    /// <summary>
    /// Startup validasyonunda fail-fast davranisi.
    /// false (onerilen): kontrollu kilit. Uygulama ayaga kalkar, business endpoint'leri middleware kapatir,
    /// lisans yenileme endpoint'leri erisilebilir kalir.
    /// true: Production'da gecersiz lisansta uygulama baslatilmaz.
    /// </summary>
    public bool FailFastOnStartupInProduction { get; set; } = false;

    /// <summary>
    /// PRODUCTION GUVENLIGI: Public key yalnizca uygulamaya gomulmus halde (EcdsaLicenseSignatureVerifier.PublicKeyParts)
    /// kullanilir. Development senaryolari icin farkli bir key test edilmek istenirse
    /// AllowPublicKeyOverride=true olmali ve PublicKeyOverride doldurulmalidir.
    /// Production'da bu alanin etkisi AddTodLicensing extension'inda EnsureProductionSafe ile engellenir.
    /// </summary>
    public bool AllowPublicKeyOverride { get; set; } = false;

    /// <summary>Yalnizca AllowPublicKeyOverride=true iken dikkate alinir (Base64-SPKI).</summary>
    public string PublicKeyOverride { get; set; } = string.Empty;

    /// <summary>
    /// Production'da integrity hash listesi bos ise lisans validasyonu hata versin mi.
    /// Onerilen: true.
    /// </summary>
    public bool RequireIntegrityHashesInProduction { get; set; } = true;

    /// <summary>
    /// Assembly hash tablosu. Key: dosya adi (or: "TOD.Platform.Licensing.dll"), Value: SHA256(Base64).
    /// CI/CD asamasinda doldurulmasi onerilir.
    /// </summary>
    public Dictionary<string, string> IntegrityHashes { get; set; } = [];
}
