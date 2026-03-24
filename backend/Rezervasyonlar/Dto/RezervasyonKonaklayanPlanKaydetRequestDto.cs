using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKonaklayanPlanKaydetRequestDto
{
    public List<RezervasyonKonaklayanKisiKaydetDto> Konaklayanlar { get; set; } = [];
}

public class RezervasyonKonaklayanKisiKaydetDto
{
    [Range(1, int.MaxValue)]
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

    public List<RezervasyonKonaklayanKisiAtamaKaydetDto> Atamalar { get; set; } = [];
}

public class RezervasyonKonaklayanKisiAtamaKaydetDto
{
    [Range(1, int.MaxValue)]
    public int SegmentId { get; set; }

    public int? OdaId { get; set; }

    public int? YatakNo { get; set; }
}
