using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampAkrabalikTipi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool YakindanDogrulanabilirMi { get; set; }

    public bool BasvuruSahibiAkrabaligiMi { get; set; }

    public bool AktifMi { get; set; } = true;
}
