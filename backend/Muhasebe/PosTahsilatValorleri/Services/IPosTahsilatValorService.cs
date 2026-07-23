using STYS.Muhasebe.PosTahsilatValorleri.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

public interface IPosTahsilatValorService : IBaseRdbmsService<PosTahsilatValorDto, PosTahsilatValor, int>
{
    Task<PosTahsilatValorOzetDto> GetOzetAsync(int? tesisId, CancellationToken cancellationToken = default);

    Task<PosTahsilatValorTopluOnayBilgisiDto> GetTopluOnayBilgisiAsync(PosTahsilatValorTopluOnayBilgisiRequest request, CancellationToken cancellationToken = default);
}
