using STYS.MisafirTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Fiyatlandirma.Entities;

public class IndirimKuraliMisafirTipi : BaseEntity<int>
{
    public int IndirimKuraliId { get; set; }

    public int MisafirTipiId { get; set; }

    public IndirimKurali? IndirimKurali { get; set; }

    public MisafirTipi? MisafirTipi { get; set; }
}
