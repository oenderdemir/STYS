using STYS.Entegrasyonlar.Pos.Dtos;

namespace STYS.Entegrasyonlar.Pos.Services;

public interface IPosPaymentTestService
{
    Task<IReadOnlyCollection<PosOdemeIslemiDto>> GetRecentAsync(int cihazId, int take, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto> StartAsync(int cihazId, PosPaymentBaslatRequest request, string requestedBy, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto> GetResultAsync(int cihazId, int posOdemeIslemiId, string requestedBy, CancellationToken cancellationToken);
    Task<PosOdemeIslemiDto> RecoverReceiptsAsync(int cihazId, int posOdemeIslemiId, string requestedBy, CancellationToken cancellationToken);
}
