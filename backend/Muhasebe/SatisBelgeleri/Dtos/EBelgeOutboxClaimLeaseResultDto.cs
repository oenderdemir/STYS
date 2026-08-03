using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri.Dtos;

public sealed record EBelgeOutboxClaimLeaseResultDto
{
    public int OutboxMesajiId { get; init; }

    public int KurumId { get; init; }

    public int EBelgeKaydiId { get; init; }

    public EBelgeOutboxIsTuru IsTuru { get; init; }

    public EBelgeOutboxDurumu Durum { get; init; }

    public int DenemeSayisi { get; init; }

    public string KilitToken { get; init; } = string.Empty;

    public DateTime KilitBitisZamaniUtc { get; init; }

    public DateTime IslemBaslamaZamaniUtc { get; init; }

    public DateTime? SonrakiDenemeZamaniUtc { get; init; }
}
