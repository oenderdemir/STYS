using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.MisafirTipleri.Entities;

public class TesisMisafirTipi : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int MisafirTipiId { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public MisafirTipi? MisafirTipi { get; set; }
}
