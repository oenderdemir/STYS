namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKpiTrendGunDto
{
    public DateTime Tarih { get; set; }

    public decimal Gelir { get; set; }

    public int RezervasyonSayisi { get; set; }

    public int IptalSayisi { get; set; }

    public int SatilanGeceSayisi { get; set; }

    public decimal DolulukOraniYuzde { get; set; }
}
