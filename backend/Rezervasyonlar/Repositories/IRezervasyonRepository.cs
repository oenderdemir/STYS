using STYS.Rezervasyonlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Rezervasyonlar.Repositories;

public interface IRezervasyonRepository : IBaseRdbmsRepository<Rezervasyon, int>
{
}
