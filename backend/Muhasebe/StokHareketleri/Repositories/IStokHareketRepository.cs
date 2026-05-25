using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokHareketleri.Repositories;

public interface IStokHareketRepository : IBaseRdbmsRepository<StokHareket, int>
{
    Task<List<StokBakiyeDto>> GetDepoStokBakiyeleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default);
    Task<List<StokKartOzetDto>> GetStokKartOzetleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default);
}
