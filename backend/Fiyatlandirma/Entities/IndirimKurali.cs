using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Fiyatlandirma.Entities;

public class IndirimKurali : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string IndirimTipi { get; set; } = IndirimTipleri.Yuzde;

    public decimal Deger { get; set; }

    [Required]
    [MaxLength(16)]
    public string KapsamTipi { get; set; } = IndirimKapsamTipleri.Sistem;

    public int? TesisId { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public int Oncelik { get; set; }

    public bool BirlesebilirMi { get; set; } = true;

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public ICollection<IndirimKuraliMisafirTipi> MisafirTipiKisitlari { get; set; } = [];

    public ICollection<IndirimKuraliKonaklamaTipi> KonaklamaTipiKisitlari { get; set; } = [];
}
