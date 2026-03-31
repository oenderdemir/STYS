using System.ComponentModel.DataAnnotations;
using STYS.Fiyatlandirma.Dto;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKaydetRequestDto
{
    [Range(1, int.MaxValue)]
    public int TesisId { get; set; }

    [Range(1, int.MaxValue)]
    public int KisiSayisi { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int MisafirTipiId { get; set; }

    [Range(1, int.MaxValue)]
    public int KonaklamaTipiId { get; set; }

    [Required]
    public DateTime GirisTarihi { get; set; }

    [Required]
    public DateTime CikisTarihi { get; set; }

    public bool TekKisilikFiyatUygulansinMi { get; set; }

    [Required]
    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    [Required]
    public string MisafirTelefon { get; set; } = string.Empty;

    public string? MisafirEposta { get; set; }

    public string? TcKimlikNo { get; set; }

    public string? PasaportNo { get; set; }

    public string? MisafirCinsiyeti { get; set; }

    public string? Notlar { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ToplamBazUcret { get; set; }

    [Range(0, double.MaxValue)]
    public decimal ToplamUcret { get; set; }

    [Required]
    [MaxLength(3)]
    public string ParaBirimi { get; set; } = "TRY";

    public List<UygulananIndirimDto> UygulananIndirimler { get; set; } = [];

    public List<RezervasyonKaydetSegmentDto> Segmentler { get; set; } = [];
}
