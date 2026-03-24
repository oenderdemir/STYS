using STYS.Fiyatlandirma.Dto;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDetayDto
{
    public int Id { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public int TesisId { get; set; }

    public string RezervasyonDurumu { get; set; } = string.Empty;

    public string MisafirAdiSoyadi { get; set; } = string.Empty;

    public string? MisafirCinsiyeti { get; set; }

    public int KisiSayisi { get; set; }

    public DateTime GirisTarihi { get; set; }

    public DateTime CikisTarihi { get; set; }

    public decimal KonaklamaUcreti { get; set; }

    public decimal EkHizmetToplami { get; set; }

    public decimal ToplamBazUcret { get; set; }

    public decimal ToplamUcret { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<UygulananIndirimDto> UygulananIndirimler { get; set; } = [];

    public List<RezervasyonEkHizmetDto> EkHizmetler { get; set; } = [];

    public List<RezervasyonDetaySegmentDto> Segmentler { get; set; } = [];
}
