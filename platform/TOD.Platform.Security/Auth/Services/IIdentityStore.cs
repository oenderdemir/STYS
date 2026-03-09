using TOD.Platform.Security.Auth.Models;

namespace TOD.Platform.Security.Auth.Services;

public interface IIdentityStore<TKey> where TKey : struct
{
    Task<IdentityUser<TKey>?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IdentityUser<TKey>?> FindByIdAsync(TKey userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetPermissionsAsync(TKey userId, CancellationToken cancellationToken = default);

    Task UpdatePasswordHashAsync(TKey userId, string newPasswordHash, CancellationToken cancellationToken = default);

    Task<IssuedRefreshToken> IssueRefreshTokenAsync(TKey userId, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult<TKey>> RotateRefreshTokenAsync(
        string refreshToken,
        DateTime nowUtc,
        DateTime newExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task RevokeAllRefreshTokensAsync(TKey userId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default);

    Task<int?> GetTokenVersionAsync(TKey userId, CancellationToken cancellationToken = default);
}
