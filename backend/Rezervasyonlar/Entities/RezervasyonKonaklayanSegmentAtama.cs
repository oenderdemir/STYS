using STYS.Odalar.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonKonaklayanSegmentAtama : BaseEntity<int>
{
    public int RezervasyonKonaklayanId { get; set; }

    public int RezervasyonSegmentId { get; set; }

    public int OdaId { get; set; }

    public RezervasyonKonaklayan? RezervasyonKonaklayan { get; set; }

    public RezervasyonSegment? RezervasyonSegment { get; set; }

    public Oda? Oda { get; set; }
}
