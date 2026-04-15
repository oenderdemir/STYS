using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Repositories;

public interface ITahsilatOdemeBelgesiRepository : IBaseRdbmsRepository<TahsilatOdemeBelgesi, int>
{
    Task<List<TahsilatOdemeBelgesi>> GetGunlukAsync(DateTime gun, CancellationToken cancellationToken = default);
}

