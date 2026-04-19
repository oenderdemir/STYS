using STYS.Muhasebe.TasinirKodlari.Dtos;
using STYS.Muhasebe.TasinirKodlari.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKodlari.Services;

public interface ITasinirKodService : IBaseRdbmsService<TasinirKodDto, TasinirKod, int>
{
    Task<List<TasinirKodDto>> GetTreeRootsAsync(CancellationToken cancellationToken = default);
    Task<List<TasinirKodDto>> GetTreeChildrenAsync(int? parentId, CancellationToken cancellationToken = default);
    Task<PagedResult<TasinirKodDto>> GetPagedForLookupAsync(PagedRequest request, string? query, CancellationToken cancellationToken = default);
    Task<TasinirKodImportSonucDto> ImportAsync(ImportTasinirKodlariRequest request, CancellationToken cancellationToken = default);
}
