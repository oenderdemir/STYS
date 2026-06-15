using System.ComponentModel.DataAnnotations;
using STYS.Kamp.Entities;
using STYS.Rezervasyonlar.Entities;
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

    public ICollection<KampProgrami> KampProgramlari { get; set; } = [];

    public ICollection<KampDonemi> KampDonemleri { get; set; } = [];

    public ICollection<Tesis> Tesisler { get; set; } = [];

    public ICollection<Rezervasyon> Rezervasyonlar { get; set; } = [];
}
