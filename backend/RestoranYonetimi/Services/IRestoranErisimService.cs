using STYS.Restoranlar.Entities;

namespace STYS.RestoranYonetimi.Services;

public interface IRestoranErisimService
{
    Task<IReadOnlyCollection<int>?> GetYetkiliRestoranIdleriAsync(CancellationToken cancellationToken = default);
    Task<IQueryable<Restoran>> ApplyRestoranScopeAsync(IQueryable<Restoran> query, CancellationToken cancellationToken = default);
    Task EnsureRestoranErisimiAsync(int restoranId, CancellationToken cancellationToken = default);
}
