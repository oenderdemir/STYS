using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.StokHareketleri.Entities;

public class StokHareket : BaseEntity<int>
{
    public int DepoId { get; set; }

    public int TasinirKartId { get; set; }

    public DateTime HareketTarihi { get; set; }

    public string HareketTipi { get; set; } = StokHareketTipleri.Giris;

    public decimal Miktar { get; set; }

    public decimal BirimFiyat { get; set; }

    public decimal Tutar { get; set; }

    public string? BelgeNo { get; set; }

    public DateTime? BelgeTarihi { get; set; }

    public string? Aciklama { get; set; }

    public int? CariKartId { get; set; }

    public string? KaynakModul { get; set; }

    public int? KaynakId { get; set; }

    public string Durum { get; set; } = StokHareketDurumlari.Aktif;

    public Depo? Depo { get; set; }
    public TasinirKart? TasinirKart { get; set; }
    public CariKart? CariKart { get; set; }
}
