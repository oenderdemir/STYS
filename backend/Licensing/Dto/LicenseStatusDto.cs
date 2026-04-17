namespace STYS.Licensing.Dto;

public class LicenseStatusDto
{
    public bool IsValid { get; set; }
    public string? LicenseId { get; set; }
    public string? ProductCode { get; set; }
    public string? CustomerCode { get; set; }
    public string? CustomerName { get; set; }
    public string? EnvironmentName { get; set; }
    public string? InstanceId { get; set; }
    public DateTimeOffset? IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public List<string> EnabledModules { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
