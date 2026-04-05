using System.Linq.Expressions;
using STYS.Kamp.Dto;
using STYS.Kamp.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.Persistence.Rdbms.Services;

namespace STYS.Kamp.Services;

public interface IKampDonemiService : IBaseRdbmsService<KampDonemiDto, KampDonemi, int>
{
    Task<KampDonemiYonetimBaglamDto> GetYonetimBaglamAsync(CancellationToken cancellationToken = default);

    Task<List<KampDonemiTesisAtamaDto>> GetTesisAtamalariAsync(int kampDonemiId, CancellationToken cancellationToken = default);

    Task<List<KampDonemiTesisAtamaDto>> KaydetTesisAtamalariAsync(int kampDonemiId, IReadOnlyCollection<KampDonemiTesisAtamaKayitDto> kayitlar, CancellationToken cancellationToken = default);

    Task<IEnumerable<KampDonemiDto>> GetAllAsync(Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null, CancellationToken cancellationToken = default);

    Task<KampDonemiDto?> GetByIdAsync(int id, Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null, CancellationToken cancellationToken = default);

    Task<PagedResult<KampDonemiDto>> GetPagedAsync(PagedRequest request, Expression<Func<KampDonemi, bool>>? predicate = null, Func<IQueryable<KampDonemi>, IQueryable<KampDonemi>>? include = null, Func<IQueryable<KampDonemi>, IOrderedQueryable<KampDonemi>>? orderBy = null, CancellationToken cancellationToken = default);
}
