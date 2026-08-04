namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>appsettings/env üzerinden yapılandırılan Schematron sidecar bağlantı ayarları (bkz. EBelgeSchematronSidecar__* env değişkenleri, docker-compose.yml).</summary>
public sealed class EBelgeSchematronSidecarOptions
{
    /// <summary>
    /// Sidecar'a gönderilen ve sidecar'ın kabul ettiği tek rule-set kimliği (whitelist). "/EARSIV"
    /// eki, GİB'in resmî UBL-TR_Main_Schematron.xml içindeki kök seviye $type parametresinin
    /// (ISO Schematron skeleton'ının resmî derleme davranışı gereği xsl:param olarak derlenir)
    /// hangi değere ("earchive") bağlanacağını sidecar'a bildirir - keyfî bir XPath/parametre
    /// DEĞİL, sabit whitelist edilmiş bir profil kimliğidir (bkz. hazırlık raporu, "Schematron
    /// profil seçimi düzeltmesi"). İlk dalgada yalnız e-Arşiv (EARSIV) desteklenir.
    /// </summary>
    public const string SupportedRuleSetId = "GIB-UBL-TR-1.2.1/2026-09-14/EARSIV";

    /// <summary>Yanıt gövdesi için üst sınır - beklenmeyen büyüklükte yanıtlar reddedilir.</summary>
    public const int MaxResponseBytes = 1_000_000;

    public string BaseUrl { get; set; } = "http://schematron-validator:8081";

    public int RequestTimeoutSeconds { get; set; } = 8;
}
