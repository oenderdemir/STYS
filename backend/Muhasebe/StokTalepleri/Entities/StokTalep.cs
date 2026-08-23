using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.Depolar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokTalepleri.Entities;

public class StokTalep : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int TalepEdenDepoId { get; set; }
    public int KarsilayanDepoId { get; set; }
    public DateTime TalepTarihi { get; set; }

    [Required]
    [MaxLength(24)]
    public string Durum { get; set; } = StokTalepDurumlari.Taslak;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public Guid? TalepEdenKullaniciId { get; set; }

    public Depo? TalepEdenDepo { get; set; }
    public Depo? KarsilayanDepo { get; set; }
    public ICollection<StokTalepSatir> Satirlar { get; set; } = [];
}
