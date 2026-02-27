using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.IsletmeAlanlari.Dto;

public class IsletmeAlaniDto : BaseRdbmsDto<int>
{
    [Required]
    public string Ad { get; set; } = string.Empty;

    [Required]
    public int BinaId { get; set; }

    public bool AktifMi { get; set; } = true;
}