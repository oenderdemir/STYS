using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Entities;

public class StokMaliyetKatmanTuketimi : BaseEntity<int>
{
    public int CikisStokHareketId { get; set; }
    public int StokMaliyetKatmaniId { get; set; }
    public decimal Miktar { get; set; }
    public decimal BirimMaliyet { get; set; }
    public decimal Tutar { get; set; }

    public StokHareket? CikisStokHareket { get; set; }
    public StokMaliyetKatmani? StokMaliyetKatmani { get; set; }
}
