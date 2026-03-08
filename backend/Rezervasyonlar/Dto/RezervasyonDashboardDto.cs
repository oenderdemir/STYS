namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDashboardDto
{
    public int TesisId { get; set; }

    public DateTime Tarih { get; set; }

    public int ToplamOdaSayisi { get; set; }

    public int DoluOdaSayisi { get; set; }

    public int BosOdaSayisi { get; set; }

    public List<RezervasyonDashboardKayitDto> BugunCheckInler { get; set; } = [];

    public List<RezervasyonDashboardKayitDto> BugunCheckOutlar { get; set; } = [];
}
