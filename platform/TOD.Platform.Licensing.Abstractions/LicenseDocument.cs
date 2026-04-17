using System.Text.Json.Serialization;

namespace TOD.Platform.Licensing.Abstractions;

/// <summary>
/// Lisans dosyasının tüm bilgilerini temsil eden belge modeli.
/// JSON olarak serialize/deserialize edilir.
/// </summary>
public sealed class LicenseDocument
{
    /// <summary>Lisansın benzersiz kimliği (GUID). Takip ve iptal için kullanılır.</summary>
    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>Ürün kodu. Hangi ürün için üretildiğini belirler (ör: "STYS").</summary>
    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>Müşteri kodu. Lisansın hangi kurum/müşteriye ait olduğunu tanımlar.</summary>
    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>Müşteri adı. Görüntüleme amaçlıdır.</summary>
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Ortam adı (ör: "Production", "Staging"). Lisansın hangi ortamda geçerli olduğunu belirler.</summary>
    [JsonPropertyName("environmentName")]
    public string EnvironmentName { get; set; } = string.Empty;

    /// <summary>Instance ID. Aynı ortamda birden fazla instance varsa ayırt etmek için kullanılır.</summary>
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Lisansın üretildiği tarih (UTC).</summary>
    [JsonPropertyName("issuedAtUtc")]
    public DateTimeOffset IssuedAtUtc { get; set; }

    /// <summary>Lisansın son kullanma tarihi (UTC). Bu tarihten sonra lisans geçersiz sayılır.</summary>
    [JsonPropertyName("expiresAtUtc")]
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>
    /// Ortam parmak izi hash'i. Lisansın sadece belirli ortamda çalışmasını sağlar.
    /// Üretim sırasında hesaplanır, doğrulama sırasında runtime değeriyle karşılaştırılır.
    /// </summary>
    [JsonPropertyName("fingerprintHash")]
    public string FingerprintHash { get; set; } = string.Empty;

    /// <summary>
    /// Bu lisansla aktif olan modüller. Modül bazlı lisanslama için kullanılır.
    /// Boş veya null ise tüm modüller aktif sayılır.
    /// </summary>
    [JsonPropertyName("enabledModules")]
    public List<string> EnabledModules { get; set; } = [];

    /// <summary>Lisans format versiyonu. İleriye dönük uyumluluk için kullanılır.</summary>
    [JsonPropertyName("licenseVersion")]
    public int LicenseVersion { get; set; } = 1;

    /// <summary>
    /// Dijital imza. Signature hariç tüm alanların canonical JSON payload'ının
    /// private key ile imzalanmış hali. Base64 kodlu.
    /// </summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}
