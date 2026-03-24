using System.ComponentModel.DataAnnotations;
using STYS.Odalar.Entities;
using STYS.Rezervasyonlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.EkHizmetler.Entities;

public class RezervasyonEkHizmet : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public int RezervasyonKonaklayanId { get; set; }

    public int EkHizmetTarifeId { get; set; }

    public int RezervasyonSegmentId { get; set; }

    public int OdaId { get; set; }

    public int? YatakNoSnapshot { get; set; }

    [Required]
    [MaxLength(128)]
    public string TarifeAdiSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BirimAdiSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string OdaNoSnapshot { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BinaAdiSnapshot { get; set; } = string.Empty;

    public DateTime HizmetTarihi { get; set; }

    public decimal Miktar { get; set; }

    public decimal BirimFiyat { get; set; }

    public decimal ToplamTutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }

    public RezervasyonKonaklayan? RezervasyonKonaklayan { get; set; }

    public EkHizmetTarife? EkHizmetTarife { get; set; }

    public RezervasyonSegment? RezervasyonSegment { get; set; }

    public Oda? Oda { get; set; }
}
