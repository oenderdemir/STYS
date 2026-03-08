namespace STYS.Rezervasyonlar.Dto;

public class OdemeRaporSatirDto
{
    public int TesisId { get; set; }

    public string TesisAdi { get; set; } = string.Empty;

    public string RezervasyonNo { get; set; } = string.Empty;

    public string OdemeYapan { get; set; } = string.Empty;

    public decimal ToplamBazUcret { get; set; }

    public decimal ToplamIndirim { get; set; }

    public decimal ToplamOdeme { get; set; }
}
