using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeHesapPlanlari.Repositories;

public interface IMuhasebeHesapPlaniRepository : IBaseRdbmsRepository<MuhasebeHesapPlani, int>
{
    Task<List<MuhasebeHesapPlani>> GetRootNodesAsync(CancellationToken cancellationToken = default);
    Task<List<MuhasebeHesapPlani>> GetChildrenByParentIdAsync(int parentId, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(string parentTamKod, int parentLevel, CancellationToken cancellationToken = default);
}
