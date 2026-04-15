using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.Depolar.Entities;

public class Depo : BaseEntity<int>
{
    public int? TesisId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Tesis? Tesis { get; set; }
    public ICollection<StokHareket> StokHareketleri { get; set; } = [];
}
