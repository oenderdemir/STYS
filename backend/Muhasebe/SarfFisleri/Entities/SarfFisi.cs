using System.ComponentModel.DataAnnotations;
using STYS.IsletmeAlanlari.Entities;
using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SarfFisleri.Entities;

public class SarfFisi : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public DateTime SarfTarihi { get; set; }
    public int? IsletmeAlaniId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = SarfFisiDurumlari.Taslak;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Guid? OlusturanKullaniciId { get; set; }
    public DateTime? IptalTarihi { get; set; }
    public Guid? IptalEdenKullaniciId { get; set; }

    [MaxLength(1024)]
    public string? IptalAciklamasi { get; set; }

    public Depo? Depo { get; set; }
    public IsletmeAlani? IsletmeAlani { get; set; }
    public ICollection<SarfFisiSatir> Satirlar { get; set; } = [];
}
