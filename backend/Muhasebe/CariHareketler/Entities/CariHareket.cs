using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.CariHareketler.Entities;

public class CariHareket : BaseEntity<int>
{
    public int CariKartId { get; set; }

    public DateTime HareketTarihi { get; set; }

    [Required]
    [MaxLength(32)]
    public string BelgeTuru { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? BelgeNo { get; set; }

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    public decimal BorcTutari { get; set; }

    public decimal AlacakTutari { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public DateTime? VadeTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = CariHareketDurumlari.Aktif;

    [MaxLength(64)]
    public string? KaynakModul { get; set; }

    public int? KaynakId { get; set; }

    public CariKart? CariKart { get; set; }
}

