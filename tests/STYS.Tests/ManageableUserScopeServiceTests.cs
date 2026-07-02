using Microsoft.EntityFrameworkCore;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Kullanicilar.Entities;
using STYS.Kullanicilar.Services;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Tests;

public class ManageableUserScopeServiceTests
{
    private static readonly Guid ActorUserId = Guid.NewGuid();

    [Fact]
    public async Task GetManageableUserIds_WhenUnrestricted_ReturnsNull()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var service = CreateService(stysDb, identityDb, UserActorScope.Unrestricted(), ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task CanManageUser_WhenUnrestricted_ReturnsTrue()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var service = CreateService(stysDb, identityDb, UserActorScope.Unrestricted(), ActorUserId);

        var result = await service.CanManageUserAsync(Guid.NewGuid());

        Assert.True(result);
    }

    [Fact]
    public async Task GetManageableUserIds_WhenScopedWithNoManagedTesisler_ReturnsNull()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        // Actor is IsTesisManagerScoped but has no actual managed tesis IDs (kurum-level admin scenario).
        // AccessScopeProvider sets ManagedTesisIds=[] in this case and returns Unrestricted,
        // so the service receives an Unrestricted scope → returns null.
        var actorScope = UserActorScope.TesisManagerScoped(managedTesisIds: [], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetManageableUserIds_WhenScopedActorHasPermission_TargetInManagedTesis_TargetIsIncluded()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int tesisId = 1;

        // Service only queries KullaniciTesisSahiplikleri — no need to seed actual Tesis entity
        await SeedOwnershipAsync(stysDb, targetUserId, tesisId);
        await SeedUserWithMarkerAsync(identityDb, targetUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.NotNull(result);
        Assert.Contains(targetUserId, result);
    }

    [Fact]
    public async Task GetManageableUserIds_WhenActorLacksPermission_TargetIsNotIncluded()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int tesisId = 1;

        await SeedOwnershipAsync(stysDb, targetUserId, tesisId);
        await SeedUserWithMarkerAsync(identityDb, targetUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        // Actor has NO permission to assign — no identity data for actor

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetManageableUserIds_WhenTargetInDifferentTesis_TargetIsNotIncluded()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int managedTesisId = 1;
        const int otherTesisId = 2;

        // Target belongs to a tesis the actor does NOT manage
        await SeedOwnershipAsync(stysDb, targetUserId, otherTesisId);
        await SeedUserWithMarkerAsync(identityDb, targetUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [managedTesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.NotNull(result);
        Assert.DoesNotContain(targetUserId, result);
    }

    [Fact]
    public async Task GetManageableUserIds_WhenTargetHasNoMarkerRole_TargetIsNotIncluded()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int tesisId = 1;

        await SeedOwnershipAsync(stysDb, targetUserId, tesisId);
        // Target has NO assignable marker role — cannot be managed by any scoped actor
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.NotNull(result);
        Assert.DoesNotContain(targetUserId, result);
    }

    [Fact]
    public async Task GetManageableUserIds_ActorCanOnlyManageResepsiyonistler_MuhasebeciIsExcluded()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var resepsiyonistId = Guid.NewGuid();
        var muhasebeciId = Guid.NewGuid();
        var noMarkerUserId = Guid.NewGuid();
        const int tesisId = 1;

        await SeedOwnershipAsync(stysDb, resepsiyonistId, tesisId);
        await SeedOwnershipAsync(stysDb, muhasebeciId, tesisId);
        await SeedOwnershipAsync(stysDb, noMarkerUserId, tesisId);

        await SeedUserWithMarkerAsync(identityDb, resepsiyonistId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        await SeedUserWithMarkerAsync(identityDb, muhasebeciId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.MuhasebeciAtanabilir));

        // Actor can only assign resepsiyonistler, NOT muhasebeciler
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.GetManageableUserIdsAsync();

        Assert.NotNull(result);
        Assert.Contains(resepsiyonistId, result);
        Assert.DoesNotContain(muhasebeciId, result);
        Assert.DoesNotContain(noMarkerUserId, result);
    }

    [Fact]
    public async Task CanManageUser_Scoped_TargetInManageableSet_ReturnsTrue()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int tesisId = 1;

        await SeedOwnershipAsync(stysDb, targetUserId, tesisId);
        await SeedUserWithMarkerAsync(identityDb, targetUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            markerRoleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir));
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.CanManageUserAsync(targetUserId);

        Assert.True(result);
    }

    [Fact]
    public async Task CanManageUser_Scoped_TargetNotInManageableSet_ReturnsFalse()
    {
        await using var stysDb = CreateStysDbContext();
        await using var identityDb = CreateIdentityDbContext();

        var targetUserId = Guid.NewGuid();
        const int tesisId = 1;

        // Target has no ownership record — actor cannot manage them
        await SeedUserWithPermissionAsync(identityDb, ActorUserId,
            domain: nameof(StructurePermissions.KullaniciAtama),
            roleName: nameof(StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir));

        var actorScope = UserActorScope.TesisManagerScoped(
            managedTesisIds: [tesisId], managedBinaIds: [], visibleUserIds: []);
        var service = CreateService(stysDb, identityDb, actorScope, ActorUserId);

        var result = await service.CanManageUserAsync(targetUserId);

        Assert.False(result);
    }

    private static ManageableUserScopeService CreateService(
        StysAppDbContext stysDb,
        TodIdentityDbContext identityDb,
        UserActorScope actorScope,
        Guid actorUserId)
    {
        return new ManageableUserScopeService(
            stysDb,
            identityDb,
            new FakeAccessScopeProvider(actorScope),
            new FakeCurrentUserAccessor(actorUserId),
            new FakeCurrentTenantAccessor());
    }

    private static StysAppDbContext CreateStysDbContext()
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StysAppDbContext(options);
    }

    private static TodIdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<TodIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TodIdentityDbContext(options);
    }

    private static async Task SeedOwnershipAsync(StysAppDbContext db, Guid userId, int tesisId)
    {
        db.KullaniciTesisSahiplikleri.Add(new KullaniciTesisSahiplik
        {
            UserId = userId,
            TesisId = tesisId
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUserWithMarkerAsync(
        TodIdentityDbContext db,
        Guid userId,
        string domain,
        string markerRoleName)
    {
        var roleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        db.Set<Role>().Add(new Role { Id = roleId, Domain = domain, Name = markerRoleName });
        db.Set<UserGroup>().Add(new UserGroup { Id = groupId, Name = $"Group-{markerRoleName}" });
        db.Set<UserGroupRole>().Add(new UserGroupRole { Id = Guid.NewGuid(), UserGroupId = groupId, RoleId = roleId });
        db.Set<UserUserGroup>().Add(new UserUserGroup { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
        await db.SaveChangesAsync();
    }

    private static async Task SeedUserWithPermissionAsync(
        TodIdentityDbContext db,
        Guid userId,
        string domain,
        string roleName)
    {
        var roleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        db.Set<Role>().Add(new Role { Id = roleId, Domain = domain, Name = roleName });
        db.Set<UserGroup>().Add(new UserGroup { Id = groupId, Name = $"Group-{roleName}" });
        db.Set<UserGroupRole>().Add(new UserGroupRole { Id = Guid.NewGuid(), UserGroupId = groupId, RoleId = roleId });
        db.Set<UserUserGroup>().Add(new UserUserGroup { Id = Guid.NewGuid(), UserId = userId, UserGroupId = groupId });
        await db.SaveChangesAsync();
    }

    private sealed class FakeAccessScopeProvider : IAccessScopeProvider
    {
        private readonly UserActorScope _actorScope;

        public FakeAccessScopeProvider(UserActorScope actorScope) => _actorScope = actorScope;

        public Task<DomainAccessScope> GetDomainAccessScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());

        public Task<UserActorScope> GetUserActorScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_actorScope);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly Guid _userId;

        public FakeCurrentUserAccessor(Guid userId) => _userId = userId;

        public string? GetCurrentUserName() => "test-actor";

        public Guid? GetCurrentUserId() => _userId;
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? GetCurrentKurumId() => null;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [];

        public bool IsSuperAdmin() => false;

        public bool IsKurumAdmin() => false;
    }
}
