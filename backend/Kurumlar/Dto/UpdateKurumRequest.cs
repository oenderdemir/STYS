namespace STYS.Kurumlar.Dto;

public class UpdateKurumRequest
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

    /// <summary>Kurum-wide agent enrollment policy. Update builds a fresh KurumDto from this
    /// request, so the value must travel here or the stored policy would be overwritten with
    /// KurumDto's `= true` default on every unrelated edit.</summary>
    public bool AgentEnrollmentRequiresApproval { get; set; } = true;

    public string? TenantKey { get; set; }

    public string? LoginHost { get; set; }
}
