namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonEkHizmetDto
{
    public int Id { get; set; }

    public int RezervasyonKonaklayanId { get; set; }

    public int EkHizmetId { get; set; }

    public int EkHizmetTarifeId { get; set; }

    public string KonaklayanAdiSoyadi { get; set; } = string.Empty;

    public string TarifeAdi { get; set; } = string.Empty;

    public DateTime HizmetTarihi { get; set; }

    public decimal Miktar { get; set; }

    public string BirimAdi { get; set; } = string.Empty;

    public decimal BirimFiyat { get; set; }

    public decimal ToplamTutar { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string OdaNo { get; set; } = string.Empty;

    public string BinaAdi { get; set; } = string.Empty;

    public int? YatakNo { get; set; }

    public string? Aciklama { get; set; }
}
