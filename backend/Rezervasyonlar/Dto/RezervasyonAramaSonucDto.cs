namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonAramaSonucDto
{
    public List<RezervasyonListeDto> Kayitlar { get; set; } = new();
    public int ToplamKayitSayisi { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
