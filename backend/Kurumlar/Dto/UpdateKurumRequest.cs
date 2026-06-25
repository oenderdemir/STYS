namespace STYS.Kurumlar.Dto;

public class UpdateKurumRequest
{
    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public string? VergiNo { get; set; }

    public string? Telefon { get; set; }

    public string? Eposta { get; set; }

    public bool AktifMi { get; set; } = true;

    public string? TenantKey { get; set; }

    public string? LoginHost { get; set; }
}
