using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

public interface ITahsilatOdemeBelgesiService : IBaseRdbmsService<TahsilatOdemeBelgesiDto, TahsilatOdemeBelgesi, int>
{
    Task<TahsilatOdemeOzetDto> GetGunlukOzetAsync(DateTime gun, CancellationToken cancellationToken = default);
}
