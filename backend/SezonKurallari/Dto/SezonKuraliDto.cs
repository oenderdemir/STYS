using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.SezonKurallari.Dto;

public class SezonKuraliDto : BaseRdbmsDto<int>
{
    [Required]
    public int TesisId { get; set; }

    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
    public string Ad { get; set; } = string.Empty;

    [Required]
    public DateTime BaslangicTarihi { get; set; }

    [Required]
    public DateTime BitisTarihi { get; set; }

    [Range(1, int.MaxValue)]
    public int MinimumGece { get; set; } = 1;

    public bool StopSaleMi { get; set; }

    public bool AktifMi { get; set; } = true;
}
