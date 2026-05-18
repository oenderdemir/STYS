using STYS.Muhasebe.MuhasebeFisleri.Dtos;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeFisleri.Repositories;

public interface IMuhasebeFisRepository : IBaseRdbmsRepository<MuhasebeFis, int>
{
    Task<MuhasebeFis?> GetByIdWithSatirlarAsync(int id, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetByKaynakAsync(string kaynakModul, int kaynakId, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<int> CountFilteredAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetYevmiyeDefteriAsync(MuhasebeFisFilterDto filter, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetMuavinDefterAsync(MuavinDefterFilterDto filter, string hesapKoduPrefix, CancellationToken cancellationToken = default);
    Task<List<MuhasebeFis>> GetMizanFisleriAsync(MizanFilterDto filter, CancellationToken cancellationToken = default);
}
