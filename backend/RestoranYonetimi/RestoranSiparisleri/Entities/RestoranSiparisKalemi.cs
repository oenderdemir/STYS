using System.ComponentModel.DataAnnotations;
using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.RestoranSiparisleri.Entities;

public class RestoranSiparisKalemi : BaseEntity<int>
{
    public int RestoranSiparisId { get; set; }

    public int RestoranMenuUrunId { get; set; }

    [Required]
    [MaxLength(128)]
    public string UrunAdiSnapshot { get; set; } = string.Empty;

    public decimal BirimFiyat { get; set; }

    public decimal Miktar { get; set; }

    public decimal SatirToplam { get; set; }

    [Required]
    [MaxLength(32)]
    public string Durum { get; set; } = RestoranSiparisKalemDurumlari.Beklemede;

    [MaxLength(512)]
    public string? Notlar { get; set; }

    public RestoranSiparis? RestoranSiparis { get; set; }

    public RestoranMenuUrun? RestoranMenuUrun { get; set; }
}
