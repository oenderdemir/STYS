using STYS.OdaTemizlik.Dto;
using TOD.Platform.Persistence.Rdbms.Paging;

namespace STYS.OdaTemizlik.Services;

public interface IOdaTemizlikService
{
    Task<List<OdaTemizlikTesisDto>> GetErisilebilirTesislerAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<OdaTemizlikKayitDto>> GetPagedAsync(
        PagedRequest request,
        string? query,
        int? tesisId,
        string? durum,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken = default);

    Task<OdaTemizlikKayitDto> BaslatTemizlikAsync(int odaId, CancellationToken cancellationToken = default);

    Task<OdaTemizlikKayitDto> TamamlaTemizlikAsync(int odaId, CancellationToken cancellationToken = default);
}
