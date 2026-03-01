using System.ComponentModel.DataAnnotations;

namespace STYS.Odalar.Dto;

public class OdaOzellikDegerDto
{
    [Required]
    public int OdaOzellikId { get; set; }

    public string? Deger { get; set; }
}
