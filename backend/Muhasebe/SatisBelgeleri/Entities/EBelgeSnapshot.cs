using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

public class EBelgeSnapshot : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int EBelgeKaydiId { get; set; }
    public EBelgeKaydi EBelgeKaydi { get; set; } = null!;

    public int BelgeVersiyonu { get; set; } = 1;

    public string SnapshotSchemaVersion { get; set; } = "1";

    public string CanonicalJson { get; set; } = string.Empty;

    public string CanonicalSha256 { get; set; } = string.Empty;
}
