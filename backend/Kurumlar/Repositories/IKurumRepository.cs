using STYS.Kurumlar.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Kurumlar.Repositories;

public interface IKurumRepository : IBaseRdbmsRepository<Kurum, int>
{
    Task<bool> ExistsByKodAsync(string kod, int? excludedId = null, CancellationToken cancellationToken = default);
}
