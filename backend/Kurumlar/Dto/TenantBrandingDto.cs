namespace STYS.Kurumlar.Dto;

public class TenantBrandingDto
{
    public int? KurumId { get; set; }
    public string? TenantKey { get; set; }
    public string? KurumAdi { get; set; }
    public string? LogoUrl { get; set; }
    public string ApplicationName { get; set; } = "STYS";
}
