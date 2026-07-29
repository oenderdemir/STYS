namespace STYS.Entegrasyonlar.Pavo.Options;

public sealed class PavoOptions
{
    public const string SectionName = "Pavo";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://overunipos-test-integration-gateway.overtech.com.tr";
    public string AppToken { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
}
