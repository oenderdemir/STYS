using STYS.Muhasebe.KasaHareketleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.KasaHareketleri.Repositories;

public interface IKasaHareketRepository : IBaseRdbmsRepository<KasaHareket, int>
{
}

