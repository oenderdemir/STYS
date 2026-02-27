using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Odalar.Dto;

public class OdaDto : BaseRdbmsDto<int>
{
    [Required]
    public string OdaNo { get; set; } = string.Empty;

    [Required]
    public int BinaId { get; set; }

    [Required]
    public int OdaTipiId { get; set; }

    public int KatNo { get; set; }

    public int? YatakSayisi { get; set; }

    public bool AktifMi { get; set; } = true;
}