using TOD.Platform.Security.Auth.Models;

namespace TOD.Platform.Security.Auth.Services;

public interface IIdentityStore<TKey> where TKey : struct
{
    Task<IdentityUser<TKey>?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetPermissionsAsync(TKey userId, CancellationToken cancellationToken = default);

    Task UpdatePasswordHashAsync(TKey userId, string newPasswordHash, CancellationToken cancellationToken = default);
}
