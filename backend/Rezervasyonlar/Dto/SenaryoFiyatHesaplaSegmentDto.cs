namespace STYS.Rezervasyonlar.Dto;

public class SenaryoFiyatHesaplaSegmentDto
{
    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public List<SenaryoFiyatHesaplaOdaAtamaDto> OdaAtamalari { get; set; } = [];
}
