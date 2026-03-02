namespace STYS.AccessScope;

public interface IAccessScopeProvider
{
    Task<DomainAccessScope> GetDomainAccessScopeAsync(CancellationToken cancellationToken = default);

    Task<UserActorScope> GetUserActorScopeAsync(CancellationToken cancellationToken = default);
}
