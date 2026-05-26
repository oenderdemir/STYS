using STYS.Muhasebe.KdvRaporlari.Dtos;

namespace STYS.Muhasebe.KdvRaporlari.Services;

public interface IKdvRaporService
{
    Task<KdvOzetRaporDto> GetOzetAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default);

    Task<TevkifatOzetRaporDto> GetTevkifatOzetAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default);

    Task<KdvHareketRaporDto> GetHareketlerAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default);

    Task<TevkifatHareketRaporDto> GetTevkifatHareketlerAsync(KdvRaporFilterDto filter, CancellationToken cancellationToken = default);
}
