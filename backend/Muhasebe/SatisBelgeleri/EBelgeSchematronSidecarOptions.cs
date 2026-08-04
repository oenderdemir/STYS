namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>appsettings/env üzerinden yapılandırılan Schematron sidecar bağlantı ayarları (bkz. EBelgeSchematronSidecar__* env değişkenleri, docker-compose.yml).</summary>
public sealed class EBelgeSchematronSidecarOptions
{
    /// <summary>Sidecar'a gönderilen ve sidecar'ın kabul ettiği tek rule-set kimliği (whitelist).</summary>
    public const string SupportedRuleSetId = "GIB-UBL-TR-1.2.1/2026-09-14";

    /// <summary>Yanıt gövdesi için üst sınır - beklenmeyen büyüklükte yanıtlar reddedilir.</summary>
    public const int MaxResponseBytes = 1_000_000;

    public string BaseUrl { get; set; } = "http://schematron-validator:8081";

    public int RequestTimeoutSeconds { get; set; } = 8;
}
