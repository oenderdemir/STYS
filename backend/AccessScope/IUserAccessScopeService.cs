namespace STYS.AccessScope;

public interface IUserAccessScopeService
{
    Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default);
}
