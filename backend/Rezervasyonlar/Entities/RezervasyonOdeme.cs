using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonOdeme : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public DateTime OdemeTarihi { get; set; } = DateTime.UtcNow;

    public decimal OdemeTutari { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required]
    [MaxLength(32)]
    public string OdemeTipi { get; set; } = OdemeTipleri.Nakit;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }
}

