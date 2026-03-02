namespace STYS.AccessScope;

public class UserAccessScopeService : IUserAccessScopeService
{
    private readonly IAccessScopeProvider _accessScopeProvider;

    public UserAccessScopeService(IAccessScopeProvider accessScopeProvider)
    {
        _accessScopeProvider = accessScopeProvider;
    }

    public async Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
    {
        return await _accessScopeProvider.GetDomainAccessScopeAsync(cancellationToken);
    }
}
