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

    [MaxLength(100)]
    public string? Ad { get; set; }

    [MaxLength(100)]
    public string? Soyad { get; set; }

    [MaxLength(24)]
    public string? KimlikTuru { get; set; }

    [MaxLength(32)]
    public string? KimlikNo { get; set; }

    [MaxLength(32)]
    public string? BelgeNo { get; set; }

    [MaxLength(32)]
    public string? BelgeTuru { get; set; }

    [MaxLength(8)]
    public string? UyrukKodu { get; set; }

    public DateTime? DogumTarihi { get; set; }

    [MaxLength(100)]
    public string? DogumYeri { get; set; }

    [MaxLength(16)]
    public string? Cinsiyet { get; set; }

    [MaxLength(32)]
    public string? Telefon { get; set; }

    [MaxLength(16)]
    public string? AracPlakasi { get; set; }

    public DateTime? FiiliGirisTarihi { get; set; }

    public DateTime? FiiliCikisTarihi { get; set; }

    [MaxLength(16)]
    public string? KonaklamaKullanimSekli { get; set; }

    [Required]
    [MaxLength(16)]
    public string KatilimDurumu { get; set; } = KonaklayanKatilimDurumlari.Bekleniyor;

    public Rezervasyon? Rezervasyon { get; set; }

    public ICollection<RezervasyonKonaklayanSegmentAtama> SegmentAtamalari { get; set; } = [];

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
