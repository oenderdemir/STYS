using STYS.RestoranMenuUrunleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMenuUrunleri.Repositories;

public interface IRestoranMenuUrunRepository : IBaseRdbmsRepository<RestoranMenuUrun, int>
{
    Task<List<RestoranMenuUrun>> GetByKategoriIdAsync(int kategoriId, CancellationToken cancellationToken = default);
}
