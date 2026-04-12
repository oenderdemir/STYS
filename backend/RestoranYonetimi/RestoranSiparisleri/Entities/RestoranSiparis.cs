using System.ComponentModel.DataAnnotations;
using STYS.Restoranlar.Entities;
using STYS.RestoranMasalari.Entities;
using STYS.RestoranOdemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranSiparisleri.Entities;

public class RestoranSiparis : BaseEntity<int>
{
    public int RestoranId { get; set; }

    public int? RestoranMasaId { get; set; }

    [Required]
    [MaxLength(64)]
    public string SiparisNo { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string SiparisDurumu { get; set; } = RestoranSiparisDurumlari.Taslak;

    public decimal ToplamTutar { get; set; }

    public decimal OdenenTutar { get; set; }

    public decimal KalanTutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required]
    [MaxLength(32)]
    public string OdemeDurumu { get; set; } = RestoranSiparisOdemeDurumlari.Odenmedi;

    [MaxLength(1024)]
    public string? Notlar { get; set; }

    public DateTime SiparisTarihi { get; set; } = DateTime.UtcNow;

    public Restoran? Restoran { get; set; }

    public RestoranMasa? RestoranMasa { get; set; }

    public ICollection<RestoranSiparisKalemi> Kalemler { get; set; } = [];

    public ICollection<RestoranOdeme> Odemeler { get; set; } = [];
}
