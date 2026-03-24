namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonEkHizmetKaydetRequestDto
{
    public int RezervasyonKonaklayanId { get; set; }

    public int EkHizmetTarifeId { get; set; }

    public DateTime HizmetTarihi { get; set; }

    public decimal Miktar { get; set; }

    public string? Aciklama { get; set; }
}
