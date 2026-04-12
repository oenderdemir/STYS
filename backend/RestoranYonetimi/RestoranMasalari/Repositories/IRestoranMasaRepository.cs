using STYS.RestoranMasalari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranMasalari.Repositories;

public interface IRestoranMasaRepository : IBaseRdbmsRepository<RestoranMasa, int>
{
    Task<List<RestoranMasa>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
}
