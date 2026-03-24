using System.ComponentModel.DataAnnotations;
using STYS.Rezervasyonlar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.EkHizmetler.Entities;

public class EkHizmetTarife : BaseEntity<int>
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

    public decimal BirimFiyat { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public ICollection<RezervasyonEkHizmet> RezervasyonEkHizmetleri { get; set; } = [];
}
