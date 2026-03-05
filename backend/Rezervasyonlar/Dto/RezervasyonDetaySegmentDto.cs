namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonDetaySegmentDto
{
    public int SegmentSirasi { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<RezervasyonDetayOdaAtamaDto> OdaAtamalari { get; set; } = [];
}
