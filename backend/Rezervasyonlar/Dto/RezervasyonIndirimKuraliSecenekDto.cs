namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonIndirimKuraliSecenekDto
{
    public int Id { get; set; }

    public string Kod { get; set; } = string.Empty;

    public string Ad { get; set; } = string.Empty;

    public string IndirimTipi { get; set; } = string.Empty;

    public decimal Deger { get; set; }

    public string KapsamTipi { get; set; } = string.Empty;

    public int Oncelik { get; set; }

    public bool BirlesebilirMi { get; set; }
}
