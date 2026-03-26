namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDashboardDto
{
    public int TesisId { get; set; }

    public DateTime Tarih { get; set; }

    public DateTime KpiBaslangicTarihi { get; set; }

    public DateTime KpiBitisTarihi { get; set; }

    public int ToplamOdaSayisi { get; set; }

    public int DoluOdaSayisi { get; set; }

    public int BosOdaSayisi { get; set; }

    public int ToplamKapasite { get; set; }

    public int KullanilanKapasite { get; set; }

    public int SerbestKapasite { get; set; }

    public RezervasyonKpiOzetDto KpiOzet { get; set; } = new();

    public List<RezervasyonGelirKirilimDto> OdemeTipineGoreGelirKirilimi { get; set; } = [];

    public List<RezervasyonGelirKirilimDto> DurumaGoreRezervasyonKirilimi { get; set; } = [];

    public List<RezervasyonKpiTrendGunDto> KpiTrendGunluk { get; set; } = [];

    public List<RezervasyonDashboardKayitDto> BugunCheckInler { get; set; } = [];

    public List<RezervasyonDashboardKayitDto> BugunCheckOutlar { get; set; } = [];
}
