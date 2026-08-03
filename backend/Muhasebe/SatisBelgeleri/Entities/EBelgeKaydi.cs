using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

public class EBelgeKaydi : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int SatisBelgesiId { get; set; }
    public SatisBelgesi SatisBelgesi { get; set; } = null!;

    public string EBelgeUuid { get; set; } = string.Empty;

    public EBelgeKanali EBelgeKanali { get; set; }

    public EBelgeKaydiDurumu Durum { get; set; } = EBelgeKaydiDurumu.SnapshotHazir;

    public EBelgeSnapshot? Snapshot { get; set; }

    public ICollection<EBelgeOutboxMesaji> OutboxMesajlari { get; set; } = new List<EBelgeOutboxMesaji>();
}
