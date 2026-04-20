using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.PaketTurleri.Entities;

public class PaketTuru : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string KisaAd { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;
}

