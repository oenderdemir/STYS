using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Tesisler.Dto;

public class TesisDto : BaseRdbmsDto<int>
{
    [Required]
    public string Ad { get; set; } = string.Empty;

    [Required]
    public int IlId { get; set; }

    [Required]
    public string Telefon { get; set; } = string.Empty;

    [Required]
    public string Adres { get; set; } = string.Empty;

    public string? Eposta { get; set; }

    public bool AktifMi { get; set; } = true;

    public ICollection<Guid>? YoneticiUserIds { get; set; }

    public ICollection<Guid>? ResepsiyonistUserIds { get; set; }
}
