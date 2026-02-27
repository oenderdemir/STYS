using System.ComponentModel.DataAnnotations;
using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaTipleri.Entities;

public class OdaTipi : BaseEntity<int>
{
    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool PaylasimliMi { get; set; }

    public int Kapasite { get; set; } = 1;

    public bool BalkonVarMi { get; set; }

    public bool KlimaVarMi { get; set; }

    public decimal? Metrekare { get; set; }

    public bool AktifMi { get; set; } = true;

    public ICollection<Oda> Odalar { get; set; } = [];
}