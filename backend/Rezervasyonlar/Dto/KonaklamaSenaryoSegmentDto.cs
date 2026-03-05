namespace STYS.Rezervasyonlar.Dto;

public class KonaklamaSenaryoSegmentDto
{
    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<KonaklamaSenaryoOdaAtamaDto> OdaAtamalari { get; set; } = [];
}
