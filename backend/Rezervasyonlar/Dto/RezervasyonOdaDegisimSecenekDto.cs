namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonOdaDegisimSecenekDto
{
    public int RezervasyonId { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public List<RezervasyonOdaDegisimKayitDto> Kayitlar { get; set; } = [];
}

