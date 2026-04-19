using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKodlari.Repositories;

public interface ITasinirKodRepository : IBaseRdbmsRepository<TasinirKod, int>
{
    Task<List<TasinirKod>> GetByTamKodlarAsync(IEnumerable<string> tamKodlar, CancellationToken cancellationToken = default);
    Task<List<TasinirKod>> GetRootNodesAsync(CancellationToken cancellationToken = default);
    Task<List<TasinirKod>> GetChildrenByParentIdAsync(int parentId, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(int parentId, CancellationToken cancellationToken = default);
}
