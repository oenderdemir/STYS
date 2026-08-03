using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

public class EBelgeOutboxMesaji : BaseEntity<int>, ITenantEntity
{
    public int KurumId { get; set; }

    public int EBelgeKaydiId { get; set; }
    public EBelgeKaydi EBelgeKaydi { get; set; } = null!;

    public EBelgeOutboxIsTuru IsTuru { get; set; }

    public EBelgeOutboxDurumu Durum { get; set; } = EBelgeOutboxDurumu.Bekliyor;

    public int DenemeSayisi { get; set; }

    public DateTime? SonrakiDenemeZamaniUtc { get; set; }

    public string? KilitToken { get; set; }

    public DateTime? KilitBitisZamaniUtc { get; set; }

    public DateTime? IslemBaslamaZamaniUtc { get; set; }

    public DateTime? TamamlanmaZamaniUtc { get; set; }

    public string? SonHataKodu { get; set; }

    public string? SonHataMesaji { get; set; }
}
