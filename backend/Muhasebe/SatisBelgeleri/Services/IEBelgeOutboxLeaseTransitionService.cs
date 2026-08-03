namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface IEBelgeOutboxLeaseTransitionService
{
    Task<bool> TryCompleteAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default);

    Task<bool> TryFailAsync(
        int outboxMesajiId,
        int kurumId,
        string kilitToken,
        string sonHataKodu,
        string sonHataMesaji,
        TimeSpan? retryDelay,
        CancellationToken cancellationToken = default);

    Task<bool> TryRenewAsync(int outboxMesajiId, int kurumId, string kilitToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
}
