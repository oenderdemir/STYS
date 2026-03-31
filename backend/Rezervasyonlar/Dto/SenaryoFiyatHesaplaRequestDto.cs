using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class SenaryoFiyatHesaplaRequestDto
{
    [Range(1, int.MaxValue)]
    public int TesisId { get; set; }

    [Range(1, int.MaxValue)]
    public int MisafirTipiId { get; set; }

    [Range(1, int.MaxValue)]
    public int KonaklamaTipiId { get; set; }

    [Required]
    public DateTime BaslangicTarihi { get; set; }

    [Required]
    public DateTime BitisTarihi { get; set; }

    public int KisiSayisi { get; set; } = 1;

    public bool TekKisilikFiyatUygulansinMi { get; set; }

    public List<SenaryoFiyatHesaplaSegmentDto> Segmentler { get; set; } = [];

    public List<int> SeciliIndirimKuraliIds { get; set; } = [];
}
