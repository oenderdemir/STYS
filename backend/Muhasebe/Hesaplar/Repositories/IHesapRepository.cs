using STYS.Muhasebe.Hesaplar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.Hesaplar.Repositories;

public interface IHesapRepository : IBaseRdbmsRepository<Hesap, int>
{
    Task<Hesap?> GetDetailByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Hesap>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByAdAsync(string ad, int? excludeId = null, CancellationToken cancellationToken = default);
}
