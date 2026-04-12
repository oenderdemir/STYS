using STYS.RestoranMenuKategorileri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMenuKategorileri.Repositories;

public interface IRestoranMenuKategoriRepository : IBaseRdbmsRepository<RestoranMenuKategori, int>
{
    Task<List<RestoranMenuKategori>> GetByRestoranIdWithUrunlerAsync(int restoranId, CancellationToken cancellationToken = default);
}
