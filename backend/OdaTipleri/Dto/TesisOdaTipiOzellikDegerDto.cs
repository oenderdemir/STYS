using System.ComponentModel.DataAnnotations;

namespace STYS.OdaTipleri.Dto;

public class TesisOdaTipiOzellikDegerDto
{
    [Required]
    public int OdaOzellikId { get; set; }

    public string? Deger { get; set; }
}
