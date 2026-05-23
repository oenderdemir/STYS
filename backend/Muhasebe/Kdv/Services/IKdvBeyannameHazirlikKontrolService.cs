using STYS.Muhasebe.Kdv.Dtos;

namespace STYS.Muhasebe.Kdv.Services;

public interface IKdvBeyannameHazirlikKontrolService
{
    Task<KdvBeyannameHazirlikKontrolDto> KontrolEtAsync(
        KdvBeyannameHazirlikKontrolFilterDto filter,
        CancellationToken cancellationToken = default);
}
