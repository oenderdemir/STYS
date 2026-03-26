using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.EkHizmetler.Entities;

public class EkHizmet : BaseEntity<int>
{
    public int TesisId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    [Required]
    [MaxLength(32)]
    public string BirimAdi { get; set; } = "Adet";

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public ICollection<EkHizmetTarife> Tarifeler { get; set; } = [];

    public ICollection<RezervasyonEkHizmet> RezervasyonEkHizmetler { get; set; } = [];
}
