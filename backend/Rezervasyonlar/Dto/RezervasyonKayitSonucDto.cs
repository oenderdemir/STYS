namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKayitSonucDto
{
    public int Id { get; set; }

    public string ReferansNo { get; set; } = string.Empty;

    public string RezervasyonDurumu { get; set; } = string.Empty;
}
