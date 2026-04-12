using STYS.Restoranlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Restoranlar.Repositories;

public interface IRestoranRepository : IBaseRdbmsRepository<Restoran, int>
{
    Task<List<Restoran>> GetByTesisIdAsync(int tesisId, CancellationToken cancellationToken = default);
}
