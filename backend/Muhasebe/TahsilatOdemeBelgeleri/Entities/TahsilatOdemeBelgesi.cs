using System.ComponentModel.DataAnnotations;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;

public class TahsilatOdemeBelgesi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string BelgeNo { get; set; } = string.Empty;

    public DateTime BelgeTarihi { get; set; }

    [Required]
    [MaxLength(16)]
    public string BelgeTipi { get; set; } = TahsilatOdemeBelgeTipleri.Tahsilat;

    public int CariKartId { get; set; }

    public decimal Tutar { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    [Required]
    [MaxLength(32)]
    public string OdemeYontemi { get; set; } = OdemeYontemleri.Nakit;

    [MaxLength(1024)]
    public string? Aciklama { get; set; }

    [MaxLength(64)]
    public string? KaynakModul { get; set; }

    public int? KaynakId { get; set; }

    [Required]
    [MaxLength(16)]
    public string Durum { get; set; } = TahsilatOdemeBelgeDurumlari.Aktif;

    public CariKart? CariKart { get; set; }
}

