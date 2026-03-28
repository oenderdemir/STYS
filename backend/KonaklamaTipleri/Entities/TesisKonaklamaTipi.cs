using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.KonaklamaTipleri.Entities;

public class TesisKonaklamaTipi : BaseEntity<int>
{
    public int TesisId { get; set; }

    public int KonaklamaTipiId { get; set; }

    public bool AktifMi { get; set; } = true;

    public Tesis? Tesis { get; set; }

    public KonaklamaTipi? KonaklamaTipi { get; set; }
}
