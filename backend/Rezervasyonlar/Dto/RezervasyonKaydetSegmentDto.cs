using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class RezervasyonKaydetSegmentDto
{
    [Required]
    public DateTime BaslangicTarihi { get; set; }

    [Required]
    public DateTime BitisTarihi { get; set; }

    public List<RezervasyonKaydetOdaAtamaDto> OdaAtamalari { get; set; } = [];
}
