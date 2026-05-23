using STYS.Muhasebe.SatisBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.SatisBelgeleri.Repositories;

public interface ISatisBelgesiRepository : IBaseRdbmsRepository<SatisBelgesi, int>
{
}
