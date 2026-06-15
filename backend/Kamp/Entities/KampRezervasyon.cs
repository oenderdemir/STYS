using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampRezervasyon : BaseEntity<int>
{
    [Required]
    [MaxLength(32)]
    public string RezervasyonNo { get; set; } = string.Empty;

    public int KampBasvuruId { get; set; }

    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    [Required]
    [MaxLength(200)]
    public string BasvuruSahibiAdiSoyadi { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string BasvuruSahibiTipi { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string KonaklamaBirimiTipi { get; set; } = string.Empty;

    public int KatilimciSayisi { get; set; }

    public decimal DonemToplamTutar { get; set; }

    public decimal AvansToplamTutar { get; set; }

    [MaxLength(32)]
    public string Durum { get; set; } = KampRezervasyonDurumlari.Aktif;

    [MaxLength(500)]
    public string? IptalNedeni { get; set; }

    public DateTime? IptalTarihi { get; set; }

    public KampDonemi? KampDonemi { get; set; }

    public Tesisler.Entities.Tesis? Tesis { get; set; }

    public KampBasvuru? KampBasvuru { get; set; }
}
