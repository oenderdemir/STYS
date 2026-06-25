using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kurumlar.Entities;

public class Kurum : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? VergiNo { get; set; }

    [MaxLength(32)]
    public string? Telefon { get; set; }

    [MaxLength(256)]
    public string? Eposta { get; set; }

    public bool AktifMi { get; set; } = true;

    [MaxLength(260)]
    public string? LogoDosyaAdi { get; set; }

    [MaxLength(260)]
    public string? LogoOrijinalDosyaAdi { get; set; }

    [MaxLength(100)]
    public string? LogoContentType { get; set; }

    public long? LogoBoyut { get; set; }

    public DateTime? LogoYuklenmeTarihi { get; set; }

    [MaxLength(64)]
    public string? TenantKey { get; set; }

    [MaxLength(256)]
    public string? LoginHost { get; set; }

    public ICollection<Tesis> Tesisler { get; set; } = [];
}
