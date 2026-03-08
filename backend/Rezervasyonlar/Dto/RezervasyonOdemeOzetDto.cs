namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdemeOzetDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public decimal ToplamUcret { get; set; }

    public decimal OdenenTutar { get; set; }

    public decimal KalanTutar { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public List<RezervasyonOdemeDto> Odemeler { get; set; } = [];
}

