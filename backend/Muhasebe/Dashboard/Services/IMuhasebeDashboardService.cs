using STYS.Muhasebe.Dashboard.Dtos;

namespace STYS.Muhasebe.Dashboard.Services;

public interface IMuhasebeDashboardService
{
    Task<MuhasebeDashboardDto> GetDashboardAsync(
        MuhasebeDashboardFilterDto filter,
        CancellationToken cancellationToken = default);
}
