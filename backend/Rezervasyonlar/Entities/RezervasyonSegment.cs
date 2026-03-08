using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Rezervasyonlar.Entities;

public class RezervasyonSegment : BaseEntity<int>
{
    public int RezervasyonId { get; set; }

    public int SegmentSirasi { get; set; }

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public Rezervasyon? Rezervasyon { get; set; }

    public ICollection<RezervasyonSegmentOdaAtama> OdaAtamalari { get; set; } = [];

    public ICollection<RezervasyonKonaklayanSegmentAtama> KonaklayanAtamalari { get; set; } = [];
}
