namespace STYS.Muhasebe.StokLotlari.Dtos;

public class StokLotBakiyeDto
{
    public int StokLotId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public DateTime? SonKullanmaTarihi { get; set; }
    public decimal GirisMiktari { get; set; }
    public decimal CikisMiktari { get; set; }
    public decimal BakiyeMiktari { get; set; }
}
