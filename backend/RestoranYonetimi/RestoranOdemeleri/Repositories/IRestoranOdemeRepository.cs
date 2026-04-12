using STYS.RestoranOdemeleri.Dtos;
using STYS.RestoranOdemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.RestoranOdemeleri.Repositories;

public interface IRestoranOdemeRepository : IBaseRdbmsRepository<RestoranOdeme, int>
{
    Task<List<RestoranOdeme>> GetBySiparisIdAsync(int siparisId, CancellationToken cancellationToken = default);
    Task<bool> HasCompletedRoomChargeAsync(int siparisId, int rezervasyonId, CancellationToken cancellationToken = default);
    Task<List<AktifRezervasyonAramaDto>> SearchAktifRezervasyonlarAsync(int tesisId, string? query, CancellationToken cancellationToken = default);
}
