namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKpiOzetDto
{
    public int TarihAraligiGunSayisi { get; set; }

    public int ToplamRezervasyonSayisi { get; set; }

    public int IptalRezervasyonSayisi { get; set; }

    public decimal IptalOraniYuzde { get; set; }

    public int ToplamGeceSayisi { get; set; }

    public int SatilanGeceSayisi { get; set; }

    public decimal DolulukOraniYuzde { get; set; }

    public decimal ToplamGelir { get; set; }

    public decimal Adr { get; set; }

    public decimal RevPar { get; set; }
}
