using STYS.Muhasebe.TasinirKartlari.Dtos;
using STYS.Muhasebe.TasinirKartlari.Entities;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.TasinirKartlari.Services;

public interface ITasinirKartService : IBaseRdbmsService<TasinirKartDto, TasinirKart, int>
{
    Task<IEnumerable<TasinirKartDto>> GetAllAsync(int? tesisId, Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null);
    Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<TasinirKartDto>> GetPagedAsync(
        TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request,
        int? tesisId,
        Func<IQueryable<TasinirKart>, IQueryable<TasinirKart>>? include = null,
        Func<IQueryable<TasinirKart>, IOrderedQueryable<TasinirKart>>? orderBy = null);
}
