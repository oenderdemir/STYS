using STYS.AccessScope;

namespace STYS.Muhasebe.Common.Services;

public interface IMuhasebeTesisScopeService
{
    Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default);
    Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default);
    Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default);
}
