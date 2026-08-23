using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;
using STYS.Muhasebe.StokMaliyetPolitikalari.Dtos;

namespace STYS.Muhasebe.StokMaliyetPolitikalari.Entities;

public class StokMaliyetKatmani : BaseEntity<int>
{
    public int TesisId { get; set; }
    public int DepoId { get; set; }
    public int TasinirKartId { get; set; }
    public int? KaynakStokHareketId { get; set; }
    public string KatmanKaynakTipi { get; set; } = StokMaliyetKatmanKaynakTipleri.StokHareketi;
    public string MaliyetYontemi { get; set; } = StokMaliyetYontemleri.FIFO;
    public DateTime GirisTarihi { get; set; }
    public decimal IlkMiktar { get; set; }
    public decimal KalanMiktar { get; set; }
    public decimal BirimMaliyet { get; set; }

    public Tesis? Tesis { get; set; }
    public Depo? Depo { get; set; }
    public TasinirKart? TasinirKart { get; set; }
    public StokHareket? KaynakStokHareket { get; set; }
    public ICollection<StokMaliyetKatmanTuketimi> Tuketimler { get; set; } = [];
}
