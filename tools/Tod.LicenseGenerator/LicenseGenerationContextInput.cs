using System.Text.Json.Serialization;

namespace Tod.LicenseGenerator;

public sealed class LicenseGenerationContextInput
{
    [JsonPropertyName("productCode")]
    public string ProductCode { get; set; } = "STYS";

    [JsonPropertyName("licenseFilePath")]
    public string LicenseFilePath { get; set; } = string.Empty;

    [JsonPropertyName("environmentName")]
    public string EnvironmentName { get; set; } = string.Empty;

    [JsonPropertyName("instanceId")]
    public string InstanceId { get; set; } = string.Empty;

    [JsonPropertyName("customerCode")]
    public string CustomerCode { get; set; } = string.Empty;

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("deploymentMarker")]
    public string DeploymentMarker { get; set; } = string.Empty;

    [JsonPropertyName("fingerprintProfile")]
    public string FingerprintProfile { get; set; } = string.Empty;

    [JsonPropertyName("runtimeFingerprintHash")]
    public string RuntimeFingerprintHash { get; set; } = string.Empty;

    [JsonPropertyName("runtimeMachineName")]
    public string RuntimeMachineName { get; set; } = string.Empty;

    [JsonPropertyName("runtimeOsDescription")]
    public string RuntimeOsDescription { get; set; } = string.Empty;

    [JsonPropertyName("requiresDeploymentMarker")]
    public bool RequiresDeploymentMarker { get; set; }
}
