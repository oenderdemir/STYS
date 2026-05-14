using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeFisleri.Repositories;

public interface IMuhasebeFisRepository : IBaseRdbmsRepository<MuhasebeFis, int>
{
    Task<MuhasebeFis?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default);
}
