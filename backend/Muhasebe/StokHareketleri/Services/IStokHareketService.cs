using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.StokHareketleri.Services;

public interface IStokHareketService : IBaseRdbmsService<StokHareketDto, StokHareket, int>
{
    Task<List<StokBakiyeDto>> GetStokBakiyeAsync(int? depoId, CancellationToken cancellationToken = default);
    Task<List<StokKartOzetDto>> GetStokKartOzetAsync(int? depoId, CancellationToken cancellationToken = default);
}
