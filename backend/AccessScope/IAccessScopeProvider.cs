namespace STYS.AccessScope;

/// <summary>
/// Scope'ların tek giriş noktasıdır.
/// Servisler scope ihtiyacını bu arayüz üzerinden karşılayarak hesaplama mantığını tek yerde toplar.
/// </summary>
public interface IAccessScopeProvider
{
    Task<DomainAccessScope> GetDomainAccessScopeAsync(CancellationToken cancellationToken = default);

    Task<UserActorScope> GetUserActorScopeAsync(CancellationToken cancellationToken = default);
}
