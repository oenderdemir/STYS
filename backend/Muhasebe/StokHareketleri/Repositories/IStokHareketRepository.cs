using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokHareketleri.Repositories;

public interface IStokHareketRepository : IBaseRdbmsRepository<StokHareket, int>
{
    Task<List<StokBakiyeDto>> GetDepoStokBakiyeleriAsync(int? depoId, CancellationToken cancellationToken = default);
    Task<List<StokKartOzetDto>> GetStokKartOzetleriAsync(int? depoId, CancellationToken cancellationToken = default);
}
