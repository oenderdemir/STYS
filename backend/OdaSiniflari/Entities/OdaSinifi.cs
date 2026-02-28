using System.ComponentModel.DataAnnotations;
using STYS.OdaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaSiniflari.Entities;

public class OdaSinifi : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool AktifMi { get; set; } = true;

    public ICollection<OdaTipi> OdaTipleri { get; set; } = [];
}
