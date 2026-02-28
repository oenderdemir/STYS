using System.ComponentModel.DataAnnotations;
using STYS.OdaSiniflari.Entities;
using STYS.Odalar.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.OdaTipleri.Entities;

public class OdaTipi : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int OdaSinifiId { get; set; }

    [Required]
    [MaxLength(128)]
    public string Ad { get; set; } = string.Empty;

    public bool PaylasimliMi { get; set; }

    public int Kapasite { get; set; } = 1;

    public bool BalkonVarMi { get; set; }

    public bool KlimaVarMi { get; set; }

    public decimal? Metrekare { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public OdaSinifi? OdaSinifi { get; set; }

    public ICollection<Oda> Odalar { get; set; } = [];
}
