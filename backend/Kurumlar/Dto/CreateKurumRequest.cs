namespace STYS.Kurumlar.Dto;

public class CreateKurumRequest
{
    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public string? VergiNo { get; set; }

    public string? VergiDairesi { get; set; }

    public string? Adres { get; set; }

    public string? Ilce { get; set; }

    public string? Il { get; set; }

    public string? Telefon { get; set; }

    public string? Eposta { get; set; }

    public bool AktifMi { get; set; } = true;

    /// <summary>Kurum-wide agent enrollment policy. Fail-safe default is true; an explicit false
    /// from the caller is honoured. Without this property AutoMapper would leave KurumDto's own
    /// `= true` initializer in place and the caller's choice would be silently discarded.</summary>
    public bool AgentEnrollmentRequiresApproval { get; set; } = true;

    public string? TenantKey { get; set; }

    public string? LoginHost { get; set; }
}
