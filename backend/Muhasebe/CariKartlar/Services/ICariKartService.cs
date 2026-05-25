using STYS.Muhasebe.CariKartlar.Dtos;
using STYS.Muhasebe.CariKartlar.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Muhasebe.CariKartlar.Services;

public interface ICariKartService : IBaseRdbmsService<CariKartDto, CariKart, int>
{
    Task<IEnumerable<CariKartDto>> GetAllAsync(int? tesisId, Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null);
    Task<PagedResult<CariKartDto>> GetPagedAsync(
        PagedRequest request,
        int? tesisId,
        Func<IQueryable<CariKart>, IQueryable<CariKart>>? include = null,
        Func<IQueryable<CariKart>, IOrderedQueryable<CariKart>>? orderBy = null);
    Task<CariBakiyeDto> GetBakiyeAsync(int cariKartId, CancellationToken cancellationToken = default);
}
