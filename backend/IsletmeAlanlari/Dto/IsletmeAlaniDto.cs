using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.IsletmeAlanlari.Dto;

public class IsletmeAlaniDto : BaseRdbmsDto<int>
{
    public string Ad { get; set; } = string.Empty;

    [Required]
    public int BinaId { get; set; }

    [Required]
    public int IsletmeAlaniSinifiId { get; set; }

    public string? IsletmeAlaniSinifiAd { get; set; }

    public string? OzelAd { get; set; }

    public bool AktifMi { get; set; } = true;
}
