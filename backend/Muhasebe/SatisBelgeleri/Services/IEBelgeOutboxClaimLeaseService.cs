using STYS.Muhasebe.SatisBelgeleri.Dtos;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public interface IEBelgeOutboxClaimLeaseService
{
    Task<EBelgeOutboxClaimLeaseResultDto?> TryClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default);
}
