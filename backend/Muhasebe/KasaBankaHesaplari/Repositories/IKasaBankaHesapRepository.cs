using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.KasaBankaHesaplari.Repositories;

public interface IKasaBankaHesapRepository : IBaseRdbmsRepository<KasaBankaHesap, int>
{
    Task<List<KasaBankaHesap>> GetByTipAsync(string tip, bool onlyActive, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKodAsync(string kod, int? excludeId = null, CancellationToken cancellationToken = default);
}
