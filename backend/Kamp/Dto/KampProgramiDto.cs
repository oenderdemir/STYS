using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Kamp.Dto;

public class KampProgramiDto : BaseRdbmsDto<int>
{
    [Required]
    public string Kod { get; set; } = string.Empty;

    [Required]
    public string Ad { get; set; } = string.Empty;

    public string? Aciklama { get; set; }

    [Required]
    public int Yil { get; set; }

    [Required]
    public int MaksimumBasvuruSayisi { get; set; } = 1;

    public bool AktifMi { get; set; } = true;
}
