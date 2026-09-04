namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonErkenCikisOzetDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public DateTime EskiCikisTarihi { get; set; }

    public DateTime YeniCikisTarihi { get; set; }

    public int EskiGeceSayisi { get; set; }

    public int YeniGeceSayisi { get; set; }

    public decimal EskiKonaklamaTutari { get; set; }

    public decimal YeniKonaklamaTutari { get; set; }

    public decimal FiyatFarki { get; set; }

    public decimal EkHizmetToplami { get; set; }

    public decimal RestoranToplami { get; set; }

    public decimal YeniToplamTutar { get; set; }

    public decimal TahsilatToplami { get; set; }

    public decimal KalanBakiye { get; set; }

    public decimal FazlaTahsilat { get; set; }

    public string ParaBirimi { get; set; } = "TRY";

    public string Mesaj { get; set; } = string.Empty;
}
