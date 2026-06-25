namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonAramaRequestDto
{
    public int? TesisId { get; set; }
    public string? AramaMetni { get; set; }
    public string? RezervasyonDurumu { get; set; }

    public DateTime? GirisBaslangicTarihi { get; set; }
    public DateTime? GirisBitisTarihi { get; set; }

    public DateTime? CikisBaslangicTarihi { get; set; }
    public DateTime? CikisBitisTarihi { get; set; }

    public bool SadeceOdemesiKalanlar { get; set; }
    public bool SadeceOdaDegisimiGerekli { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
