using STYS.Muhasebe.TevkifatHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.TevkifatHesapEslemeleri.Repositories;

public interface ITevkifatHesapEslemeRepository : IBaseRdbmsRepository<TevkifatHesapEsleme, int>
{
    Task<TevkifatHesapEsleme?> GetAktifEslemeAsync(
        int? tesisId,
        string islemYonu,
        int tevkifatPay,
        int tevkifatPayda,
        CancellationToken cancellationToken = default);
}
