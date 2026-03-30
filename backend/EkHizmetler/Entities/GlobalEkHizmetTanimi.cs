using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.EkHizmetler.Entities;

public class GlobalEkHizmetTanimi : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    [Required]
    [MaxLength(32)]
    public string BirimAdi { get; set; } = "Adet";

    [MaxLength(64)]
    public string? PaketIcerikHizmetKodu { get; set; }

    public bool AktifMi { get; set; } = true;

    public ICollection<EkHizmet> TesisAtamalari { get; set; } = [];
}
