namespace STYS.Rezervasyonlar.Dto;

public class OdemeRaporDto
{
    public List<int> TesisIds { get; set; } = [];

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<OdemeRaporSatirDto> Satirlar { get; set; } = [];

    public decimal ToplamGelir { get; set; }
}
