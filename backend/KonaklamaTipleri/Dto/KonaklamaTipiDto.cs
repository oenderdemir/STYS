using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.KonaklamaTipleri.Dto;

public class KonaklamaTipiDto : BaseRdbmsDto<int>
{
    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}
