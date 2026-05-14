using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Dtos;
using STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.MuhasebeVergiHesapEslemeleri.Services;

public interface IMuhasebeVergiHesapEslemeService : IBaseRdbmsService<MuhasebeVergiHesapEslemeDto, MuhasebeVergiHesapEsleme, int>
{
    Task<MuhasebeVergiHesapEslemeDto?> GetAktifEslemeAsync(
        string vergiTipi,
        decimal oran,
        int? tesisId,
        CancellationToken cancellationToken = default);
}
