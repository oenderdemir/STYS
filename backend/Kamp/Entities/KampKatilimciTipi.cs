using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampKatilimciTipi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool KamuTarifesiUygulanirMi { get; set; }

    public bool AktifMi { get; set; } = true;
}
