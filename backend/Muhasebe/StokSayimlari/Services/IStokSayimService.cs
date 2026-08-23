using STYS.Muhasebe.StokSayimlari.Dtos;
using STYS.Muhasebe.StokSayimlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.StokSayimlari.Services;

public interface IStokSayimService : IBaseRdbmsService<StokSayimDto, StokSayim, int>
{
    Task<StokSayimDto> UpdateSatirlarAsync(int id, UpdateStokSayimSatirlarRequest request, CancellationToken cancellationToken = default);
    Task<StokSayimDto> AddSatirAsync(int id, AddStokSayimSatirRequest request, CancellationToken cancellationToken = default);
    Task DeleteSatirAsync(int id, int satirId, CancellationToken cancellationToken = default);
    Task<StokSayimDto> RefreshAsync(int id, CancellationToken cancellationToken = default);
    Task<StokSayimDto> KesinlestirAsync(int id, CancellationToken cancellationToken = default);
    Task<StokSayimDto> IptalAsync(int id, CancellationToken cancellationToken = default);
}
