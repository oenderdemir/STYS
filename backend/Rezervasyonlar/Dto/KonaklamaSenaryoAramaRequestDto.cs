using System.ComponentModel.DataAnnotations;

namespace STYS.Rezervasyonlar.Dto;

public class KonaklamaSenaryoAramaRequestDto
{
    [Range(1, int.MaxValue)]
    public int TesisId { get; set; }

    public int? OdaTipiId { get; set; }

    [Range(1, int.MaxValue)]
    public int MisafirTipiId { get; set; }

    [Range(1, int.MaxValue)]
    public int KonaklamaTipiId { get; set; }

    [Range(1, int.MaxValue)]
    public int KisiSayisi { get; set; } = 1;

    [Required]
    public DateTime BaslangicTarihi { get; set; }

    [Required]
    public DateTime BitisTarihi { get; set; }
}
