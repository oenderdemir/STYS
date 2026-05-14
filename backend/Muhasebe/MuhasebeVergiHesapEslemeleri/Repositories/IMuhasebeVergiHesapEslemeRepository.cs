using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Repositories;

public interface IMuhasebeVergiHesapEslemeRepository : IBaseRdbmsRepository<MuhasebeVergiHesapEsleme, int>
{
    Task<MuhasebeVergiHesapEsleme?> GetAktifEslemeAsync(
        string vergiTipi,
        decimal oran,
        int? tesisId,
        CancellationToken cancellationToken = default);
}
