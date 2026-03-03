using STYS.KonaklamaTipleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Fiyatlandirma.Entities;

public class IndirimKuraliKonaklamaTipi : BaseEntity<int>
{
    public int IndirimKuraliId { get; set; }

    public int KonaklamaTipiId { get; set; }

    public IndirimKurali? IndirimKurali { get; set; }

    public KonaklamaTipi? KonaklamaTipi { get; set; }
}
