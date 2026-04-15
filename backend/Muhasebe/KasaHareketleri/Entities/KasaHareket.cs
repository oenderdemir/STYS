using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.KasaHareketleri.Entities;

public class KasaHareket : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string KasaKodu { get; set; } = string.Empty;

    public DateTime HareketTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string HareketTipi { get; set; } = KasaHareketTipleri.Tahsilat;

    public decimal Tutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    [MaxLength(64)]
    public string? BelgeNo { get; set; }

    public int? CariKartId { get; set; }

    [MaxLength(64)]
    public string? KaynakModul { get; set; }

    public int? KaynakId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = CariHareketDurumlari.Aktif;

    public CariKart? CariKart { get; set; }
}

