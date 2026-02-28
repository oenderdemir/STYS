using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.OdaSiniflari.Dto;

public class OdaSinifiDto : BaseRdbmsDto<int>
{
    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}
