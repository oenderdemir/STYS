using STYS.Muhasebe.StokTalepleri.Dtos;
using STYS.Muhasebe.StokTalepleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.StokTalepleri.Services;

public interface IStokTalepService : IBaseRdbmsService<StokTalepDto, StokTalep, int>
{
    Task<StokTalepDto> UpdateTalepSatirlariAsync(int id, UpdateTalepSatirlariRequest request, CancellationToken cancellationToken = default);
    Task<StokTalepDto> OnayMiktarlariniGuncelleAsync(int id, OnayMiktarlariniGuncelleRequest request, CancellationToken cancellationToken = default);
    Task<StokTalepDto> AddSatirAsync(int id, AddStokTalepSatirRequest request, CancellationToken cancellationToken = default);
    Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default);
    Task<StokTalepDto> GonderAsync(int id, CancellationToken cancellationToken = default);
    Task<StokTalepDto> ReddetAsync(int id, CancellationToken cancellationToken = default);
    Task<StokTalepDto> TeslimEtAsync(int id, TeslimEtStokTalepRequest request, CancellationToken cancellationToken = default);
    Task<StokTalepDto> IptalAsync(int id, CancellationToken cancellationToken = default);
}
