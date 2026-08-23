using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokSayimlari.Entities;

public class StokSayim : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public DateTime SayimTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = StokSayimDurumlari.Taslak;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Depo? Depo { get; set; }
    public ICollection<StokSayimSatir> Satirlar { get; set; } = [];
}
