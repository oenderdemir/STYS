using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Kurumlar.Dto;

public class KurumDto : BaseRdbmsDto<int>
{
    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public string? VergiNo { get; set; }

    public string? Telefon { get; set; }

    public string? Eposta { get; set; }

    public bool AktifMi { get; set; }

    public string? LogoDosyaAdi { get; set; }

    public string? LogoOrijinalDosyaAdi { get; set; }

    public string? LogoContentType { get; set; }

    public long? LogoBoyut { get; set; }

    public DateTime? LogoYuklenmeTarihi { get; set; }

    public string? LogoUrl { get; set; }

    public string? TenantKey { get; set; }

    public string? LoginHost { get; set; }
}
