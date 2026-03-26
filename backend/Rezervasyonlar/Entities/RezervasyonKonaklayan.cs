using System.ComponentModel.DataAnnotations;
using STYS.Rezervasyonlar;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonKonaklayan : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public int SiraNo { get; set; }

    [Required]
    [MaxLength(200)]
    public string AdSoyad { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? TcKimlikNo { get; set; }

    [MaxLength(32)]
    public string? PasaportNo { get; set; }

    [MaxLength(16)]
    public string? Cinsiyet { get; set; }

    [Required]
    [MaxLength(16)]
    public string KatilimDurumu { get; set; } = KonaklayanKatilimDurumlari.Bekleniyor;

    public Rezervasyon? Rezervasyon { get; set; }

    public ICollection<RezervasyonKonaklayanSegmentAtama> SegmentAtamalari { get; set; } = [];
}
