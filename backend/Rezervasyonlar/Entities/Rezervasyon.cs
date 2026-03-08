using System.ComponentModel.DataAnnotations;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class Rezervasyon : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string ReferansNo { get; set; } = string.Empty;

    public int TesisId { get; set; }

    public int KisiSayisi { get; set; } = 1;

    public int? MisafirTipiId { get; set; }

    public int? KonaklamaTipiId { get; set; }

    public DateTime GirisTarihi { get; set; }

    public DateTime CikisTarihi { get; set; }

    public decimal ToplamBazUcret { get; set; }

    public decimal ToplamUcret { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public string? UygulananIndirimlerJson { get; set; }

    [Required]
    [MaxLength(200)]
    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string MisafirTelefon { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? MisafirEposta { get; set; }

    [MaxLength(32)]
    public string? TcKimlikNo { get; set; }

    [MaxLength(32)]
    public string? PasaportNo { get; set; }

    [MaxLength(1024)]
    public string? Notlar { get; set; }

    [Required]
    [MaxLength(32)]
    public string RezervasyonDurumu { get; set; } = RezervasyonDurumlari.Onayli;

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public ICollection<RezervasyonSegment> Segmentler { get; set; } = [];

    public ICollection<RezervasyonKonaklayan> Konaklayanlar { get; set; } = [];

    public ICollection<RezervasyonOdeme> Odemeler { get; set; } = [];

    public ICollection<RezervasyonDegisiklikGecmisi> DegisiklikGecmisiKayitlari { get; set; } = [];
}
