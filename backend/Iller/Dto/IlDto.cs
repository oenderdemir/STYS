using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Iller.Dto;

public class IlDto : BaseRdbmsDto<int>
{
    [Required]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}