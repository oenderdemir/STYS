using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Security.Auth.Services;
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
            Status = user.Status.ToString()
        };
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

        user.PasswordHash = newPasswordHash;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
