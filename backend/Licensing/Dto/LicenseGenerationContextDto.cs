namespace STYS.Licensing.Dto;

public sealed class LicenseGenerationContextDto
{
    public string ProductCode { get; set; } = "STYS";

    public string LicenseFilePath { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    public string CustomerCode { get; set; } = string.Empty;

    public string DeploymentMarker { get; set; } = string.Empty;

    public string FingerprintProfile { get; set; } = string.Empty;

    public string RuntimeFingerprintHash { get; set; } = string.Empty;

    public string RuntimeMachineName { get; set; } = string.Empty;

    public string RuntimeOsDescription { get; set; } = string.Empty;

    public bool RequiresDeploymentMarker { get; set; }
}
