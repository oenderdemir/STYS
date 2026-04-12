using STYS.RestoranSiparisleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranSiparisleri.Repositories;

public interface IRestoranSiparisRepository : IBaseRdbmsRepository<RestoranSiparis, int>
{
    Task<RestoranSiparis?> GetDetayByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<RestoranSiparis>> GetByRestoranIdAsync(int restoranId, CancellationToken cancellationToken = default);
    Task<List<RestoranSiparis>> GetAcikSiparislerAsync(int? masaId, CancellationToken cancellationToken = default);
    Task<RestoranSiparis?> GetMasaAcikSiparisAsync(int masaId, CancellationToken cancellationToken = default);
}
