using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TasinirKartlari.Repositories;

public interface ITasinirKartRepository : IBaseRdbmsRepository<TasinirKart, int>
{
}
