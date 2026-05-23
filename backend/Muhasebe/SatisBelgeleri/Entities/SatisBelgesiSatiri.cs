using STYS.Muhasebe.Kdv.Entities;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

public class SatisBelgesiSatiri : BaseEntity<int>
{
    public int SatisBelgesiId { get; set; }
    public SatisBelgesi SatisBelgesi { get; set; } = null!;

    public int SiraNo { get; set; }

    public SatisBelgesiSatirTipi SatirTipi { get; set; } = SatisBelgesiSatirTipi.Diger;

    public string Aciklama { get; set; } = string.Empty;

    public decimal Miktar { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Matrah { get; set; }

    public KdvUygulamaTipi KdvUygulamaTipi { get; set; } = KdvUygulamaTipi.Kdvli;

    public int? KdvIstisnaTanimId { get; set; }
    public KdvIstisnaTanim? KdvIstisnaTanim { get; set; }

    public string? KdvIstisnaKodu { get; set; }
    public string? KdvIstisnaAciklamasi { get; set; }

    public decimal KdvOrani { get; set; }
    public decimal KdvTutari { get; set; }

    public decimal SatirToplami { get; set; }

    public string? KaynakSatirId { get; set; }
}
