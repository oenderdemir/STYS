namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonCariKartSecenekDto
{
    public int Id { get; set; }

    public string UnvanAdSoyad { get; set; } = string.Empty;

    public string? VergiNoTckn { get; set; }

    public string? Telefon { get; set; }
}
