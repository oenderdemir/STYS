using System.ComponentModel.DataAnnotations;
using STYS.Tesisler;
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

    [Required]
    public string GirisSaati { get; set; } = "14:00";

    [Required]
    public string CikisSaati { get; set; } = "10:00";

    [Required]
    public string EkHizmetPaketCakismaPolitikasi { get; set; } = EkHizmetPaketCakismaPolitikalari.OnayIste;

    public bool AktifMi { get; set; } = true;

    public ICollection<Guid>? YoneticiUserIds { get; set; }

    public ICollection<Guid>? ResepsiyonistUserIds { get; set; }
}
