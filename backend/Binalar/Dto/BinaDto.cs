using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Binalar.Dto;

public class BinaDto : BaseRdbmsDto<int>
{
    [Required]
    public string Ad { get; set; } = string.Empty;

    [Required]
    public int TesisId { get; set; }

    [Range(1, int.MaxValue)]
    public int KatSayisi { get; set; } = 1;

    public bool AktifMi { get; set; } = true;

    public ICollection<Guid>? YoneticiUserIds { get; set; }
}
