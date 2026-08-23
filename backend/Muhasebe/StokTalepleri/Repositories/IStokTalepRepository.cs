using STYS.Muhasebe.StokTalepleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokTalepleri.Repositories;

public interface IStokTalepRepository : IBaseRdbmsRepository<StokTalep, int>
{
}
