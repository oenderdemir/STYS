using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Identity.Roles.Entities;
using TOD.Platform.Identity.UserGroupRoles.Entities;
using TOD.Platform.Identity.UserGroups.Entities;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.UserUserGroups.Entities;
using TOD.Platform.Persistence.RDBMS.Entities;
using TOD.Platform.Security.Auth.Services;

namespace TOD.Platform.Identity.Infrastructure.EntityFramework;

public class TodIdentityDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUserAccessor;

    public TodIdentityDbContext(DbContextOptions<TodIdentityDbContext> options, ICurrentUserAccessor? currentUserAccessor = null)
        : base(options)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserGroup> UserGroups => Set<UserGroup>();

    public DbSet<UserUserGroup> UserUserGroups => Set<UserUserGroup>();

    public DbSet<UserGroupRole> UserGroupRoles => Set<UserGroupRole>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<MenuItemRole> MenuItemRoles => Set<MenuItemRole>();

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("TODBase");

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.UserName).HasMaxLength(128);
            entity.Property(x => x.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(x => x.Domain).HasDefaultValue(string.Empty);
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.HasIndex(x => new { x.Domain, x.Name }).IsUnique();
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<UserUserGroup>(entity =>
        {
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserUserGroups)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.UserGroup)
                .WithMany(x => x.UserUserGroups)
                .HasForeignKey(x => x.UserGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.UserId, x.UserGroupId }).IsUnique();
        });

        modelBuilder.Entity<UserGroupRole>(entity =>
        {
            entity.HasOne(x => x.UserGroup)
                .WithMany(x => x.UserGroupRoles)
                .HasForeignKey(x => x.UserGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.UserGroupRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.UserGroupId, x.RoleId }).IsUnique();
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MenuItemRole>(entity =>
        {
            entity.HasOne(x => x.MenuItem)
                .WithMany(x => x.MenuItemRoles)
                .HasForeignKey(x => x.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Role)
                .WithMany(x => x.MenuItemRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.MenuItemId, x.RoleId }).IsUnique();
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity<Guid>).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(parameter, nameof(BaseEntity<Guid>.IsDeleted));
                var body = Expression.Equal(prop, Expression.Constant(false));
                var lambda = Expression.Lambda(body, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries<BaseEntity<Guid>>();
        var now = DateTime.UtcNow;
        var user = _currentUserAccessor?.GetCurrentUserName() ?? "system";

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = user;
                    entry.Entity.IsDeleted = false;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = user;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.DeletedBy = user;
                    break;
            }
        }
    }
}
