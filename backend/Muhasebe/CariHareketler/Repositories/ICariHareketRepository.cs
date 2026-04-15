using STYS.Muhasebe.CariHareketler.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.CariHareketler.Repositories;

public interface ICariHareketRepository : IBaseRdbmsRepository<CariHareket, int>
{
    Task<List<CariHareket>> GetCariEkstresiAsync(int cariKartId, DateTime? baslangic, DateTime? bitis, CancellationToken cancellationToken = default);
}

