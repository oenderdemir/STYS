using System.ComponentModel.DataAnnotations;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaOzellikleri.Entities;

public class OdaOzellik : BaseEntity<int>
{
    [Required]
    [MaxLength(64)]
    public string Kod { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string VeriTipi { get; set; } = OdaOzellikVeriTipleri.Boolean;

    public bool AktifMi { get; set; } = true;

    public ICollection<OdaOzellikDeger> OdaDegerleri { get; set; } = [];
}
