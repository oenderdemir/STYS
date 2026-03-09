using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.RefreshTokens.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.Security.Auth.Models;
using SecurityIdentityUser = TOD.Platform.Security.Auth.Models.IdentityUser<System.Guid>;

namespace TOD.Platform.Identity.Infrastructure.Stores;

public class EfIdentityStore : IIdentityStore<Guid>
{
    private readonly TodIdentityDbContext _dbContext;

    public EfIdentityStore(TodIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SecurityIdentityUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

        return MapUser(user);
    }

    public async Task<SecurityIdentityUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        return MapUser(user);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var permissions = await _dbContext.UserUserGroups
            .Where(uug => uug.UserId == userId)
            .SelectMany(uug => uug.UserGroup.UserGroupRoles)
            .Select(ugr => ugr.Role.Domain + "." + ugr.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissions;
    }

    public async Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException("User was not found.");
        }

        user.Status = Common.Enums.UserStatus.Standard;
        user.PasswordHash = newPasswordHash;
        user.TokenVersion += 1;

        await RevokeAllRefreshTokensAsync(userId, DateTime.UtcNow, "Password changed", cancellationToken);

        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IssuedRefreshToken> IssueRefreshTokenAsync(Guid userId, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var rawToken = GenerateRefreshToken();
        var tokenHash = ComputeTokenHash(rawToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc
        };

        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken
        {
            RefreshToken = rawToken,
            ExpiresAt = expiresAtUtc
        };
    }

    public async Task<RefreshTokenRotationResult<Guid>> RotateRefreshTokenAsync(
        string refreshToken,
        DateTime nowUtc,
        DateTime newExpiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(refreshToken);
        var existingToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (existingToken is null)
        {
            return new RefreshTokenRotationResult<Guid>
            {
                Status = RefreshTokenRotationStatus.Invalid
            };
        }

        if (existingToken.RevokedAt.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(existingToken.ReplacedByTokenHash))
            {
                await InvalidateUserSessionsAsync(
                    existingToken.UserId,
                    nowUtc,
                    "Refresh token reuse detected",
                    cancellationToken);

                return new RefreshTokenRotationResult<Guid>
                {
                    Status = RefreshTokenRotationStatus.ReuseDetected,
                    UserId = existingToken.UserId
                };
            }

            return new RefreshTokenRotationResult<Guid>
            {
                Status = RefreshTokenRotationStatus.ExpiredOrRevoked,
                UserId = existingToken.UserId
            };
        }

        if (existingToken.ExpiresAt <= nowUtc)
        {
            return new RefreshTokenRotationResult<Guid>
            {
                Status = RefreshTokenRotationStatus.ExpiredOrRevoked,
                UserId = existingToken.UserId
            };
        }

        var newRawToken = GenerateRefreshToken();
        var newTokenHash = ComputeTokenHash(newRawToken);

        existingToken.RevokedAt = nowUtc;
        existingToken.ReplacedByTokenHash = newTokenHash;
        existingToken.RevokeReason = "Refresh token rotated";

        var rotatedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = newExpiresAtUtc
        };

        _dbContext.RefreshTokens.Update(existingToken);
        await _dbContext.RefreshTokens.AddAsync(rotatedToken, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotationResult<Guid>
        {
            Status = RefreshTokenRotationStatus.Rotated,
            UserId = existingToken.UserId,
            RefreshToken = newRawToken,
            ExpiresAt = newExpiresAtUtc
        };
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId, DateTime nowUtc, string reason, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && !x.RevokedAt.HasValue && x.ExpiresAt > nowUtc)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count == 0)
        {
            return;
        }

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = nowUtc;
            activeToken.RevokeReason = reason;
        }

        _dbContext.RefreshTokens.UpdateRange(activeTokens);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokenVersion = await _dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => (int?)x.TokenVersion)
            .FirstOrDefaultAsync(cancellationToken);

        return tokenVersion;
    }

    private static SecurityIdentityUser? MapUser(TOD.Platform.Identity.Users.Entities.User? user)
    {
        if (user is null)
        {
            return null;
        }

        return new SecurityIdentityUser
        {
            Id = user.Id,
            UserName = user.UserName,
            PasswordHash = user.PasswordHash,
            Name = user.FirstName,
            Surname = user.LastName,
            Email = user.Email,
            Status = user.Status.ToString(),
            TokenVersion = user.TokenVersion
        };
    }

    private static string GenerateRefreshToken()
    {
        Span<byte> bytes = stackalloc byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken.Trim());
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToHexString(hashBytes);
    }

    private async Task InvalidateUserSessionsAsync(
        Guid userId,
        DateTime nowUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is not null)
        {
            user.TokenVersion += 1;
        }

        var unrevokedTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId && !x.RevokedAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var token in unrevokedTokens)
        {
            token.RevokedAt = nowUtc;
            token.RevokeReason = reason;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
