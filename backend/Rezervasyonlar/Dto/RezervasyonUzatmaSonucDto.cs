namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonUzatmaSonucDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public string SenaryoKodu { get; set; } = string.Empty;

    /// <summary>Bkz. RezervasyonUzatmaSenaryoTipleri.</summary>
    public string SenaryoTipi { get; set; } = string.Empty;

    public DateTime EskiCikisTarihi { get; set; }

    public DateTime YeniCikisTarihi { get; set; }

    public decimal EkBazUcret { get; set; }

    public decimal EkNihaiUcret { get; set; }

    public decimal YeniToplamBazUcret { get; set; }

    public decimal YeniToplamUcret { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<KonaklamaSenaryoSegmentDto> Segmentler { get; set; } = [];

    public string Mesaj { get; set; } = string.Empty;
}
