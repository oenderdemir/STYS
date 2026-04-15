using STYS.Muhasebe.BankaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.BankaHareketleri.Repositories;

public interface IBankaHareketRepository : IBaseRdbmsRepository<BankaHareket, int>
{
}

