using STYS.Bildirimler.Dto;

namespace STYS.Bildirimler.Services;

public interface IBildirimService
{
    Task<List<BildirimDto>> GetCurrentUserBildirimlerAsync(int take = 20, CancellationToken cancellationToken = default);
    Task<int> GetCurrentUserUnreadCountAsync(CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(int bildirimId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(CancellationToken cancellationToken = default);
    Task PublishToTesisUsersAsync(int tesisId, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default);
    Task PublishToUsersAsync(IEnumerable<Guid> userIds, BildirimOlusturRequestDto request, CancellationToken cancellationToken = default);
}
