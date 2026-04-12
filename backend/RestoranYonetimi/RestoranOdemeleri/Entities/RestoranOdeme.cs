using System.ComponentModel.DataAnnotations;
using STYS.Rezervasyonlar.Entities;
using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranOdemeleri.Entities;

public class RestoranOdeme : BaseEntity<int>
{
    public int RestoranSiparisId { get; set; }

    [Required]
    [MaxLength(32)]
    public string OdemeTipi { get; set; } = RestoranOdemeTipleri.Nakit;

    public decimal Tutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public DateTime OdemeTarihi { get; set; } = DateTime.UtcNow;

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    public int? RezervasyonId { get; set; }

    public int? RezervasyonOdemeId { get; set; }

    [Required]
    [MaxLength(32)]
    public string Durum { get; set; } = RestoranOdemeDurumlari.Tamamlandi;

    [MaxLength(64)]
    public string? IslemReferansNo { get; set; }

    public RestoranSiparis? RestoranSiparis { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }

    public RezervasyonOdeme? RezervasyonOdeme { get; set; }
}
