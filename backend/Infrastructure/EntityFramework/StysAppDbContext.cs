using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using STYS.Binalar.Entities;
using STYS.Countries.Entities;
using STYS.Iller.Entities;
using STYS.IsletmeAlanlari.Entities;
using STYS.OdaSiniflari.Entities;
using STYS.Odalar.Entities;
using STYS.OdaTipleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;
using TOD.Platform.Security.Auth.Services;

namespace STYS.Infrastructure.EntityFramework;

public class StysAppDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUserAccessor;

    public StysAppDbContext(DbContextOptions<StysAppDbContext> options, ICurrentUserAccessor? currentUserAccessor = null)
        : base(options)
    {
        _currentUserAccessor = currentUserAccessor;
    }

    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Il> Iller => Set<Il>();
    public DbSet<Tesis> Tesisler => Set<Tesis>();
    public DbSet<Bina> Binalar => Set<Bina>();
    public DbSet<IsletmeAlani> IsletmeAlanlari => Set<IsletmeAlani>();
    public DbSet<OdaSinifi> OdaSiniflari => Set<OdaSinifi>();
    public DbSet<OdaTipi> OdaTipleri => Set<OdaTipi>();
    public DbSet<Oda> Odalar => Set<Oda>();

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
        modelBuilder.HasDefaultSchema("dbo");

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("Countries", "dbo");
            entity.Property(x => x.Name).HasMaxLength(128);
            entity.Property(x => x.Code).HasMaxLength(16);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Il>(entity =>
        {
            entity.ToTable("Iller", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<Tesis>(entity =>
        {
            entity.ToTable("Tesisler", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Telefon).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Adres).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Eposta).HasMaxLength(256);

            entity.HasIndex(x => new { x.IlId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Il)
                .WithMany(x => x.Tesisler)
                .HasForeignKey(x => x.IlId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Bina>(entity =>
        {
            entity.ToTable("Binalar", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.Binalar)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IsletmeAlani>(entity =>
        {
            entity.ToTable("IsletmeAlanlari", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.BinaId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Bina)
                .WithMany(x => x.IsletmeAlanlari)
                .HasForeignKey(x => x.BinaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OdaSinifi>(entity =>
        {
            entity.ToTable("OdaSiniflari", "dbo");
            entity.Property(x => x.Kod).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Kod)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
            entity.HasIndex(x => x.Ad)
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");
        });

        modelBuilder.Entity<OdaTipi>(entity =>
        {
            entity.ToTable("TesisOdaTipleri", "dbo");
            entity.Property(x => x.Ad).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Metrekare).HasColumnType("decimal(10,2)");
            entity.HasIndex(x => new { x.TesisId, x.Ad })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Tesis)
                .WithMany(x => x.OdaTipleri)
                .HasForeignKey(x => x.TesisId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OdaSinifi)
                .WithMany(x => x.OdaTipleri)
                .HasForeignKey(x => x.OdaSinifiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Oda>(entity =>
        {
            entity.ToTable("Odalar", "dbo");
            entity.Property(x => x.OdaNo).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.BinaId, x.OdaNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AktifMi] = 1");

            entity.HasOne(x => x.Bina)
                .WithMany(x => x.Odalar)
                .HasForeignKey(x => x.BinaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.TesisOdaTipi)
                .WithMany(x => x.Odalar)
                .HasForeignKey(x => x.TesisOdaTipiId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (IsBaseEntityType(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(parameter, nameof(BaseEntity<int>.IsDeleted));
                var body = Expression.Equal(prop, Expression.Constant(false));
                var lambda = Expression.Lambda(body, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries()
            .Where(x => x.Entity is not null && IsBaseEntityType(x.Entity.GetType()))
            .ToList();
        var now = DateTime.UtcNow;
        var user = _currentUserAccessor?.GetCurrentUserName() ?? "system";

        foreach (var entry in entries)
        {
            var entity = entry.Entity;
            switch (entry.State)
            {
                case EntityState.Added:
                    SetProperty(entity, nameof(BaseEntity<int>.CreatedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.CreatedBy), user);
                    SetProperty(entity, nameof(BaseEntity<int>.IsDeleted), false);
                    break;
                case EntityState.Modified:
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.UpdatedBy), user);
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    SetProperty(entity, nameof(BaseEntity<int>.IsDeleted), true);
                    SetProperty(entity, nameof(BaseEntity<int>.DeletedAt), now);
                    SetProperty(entity, nameof(BaseEntity<int>.DeletedBy), user);
                    break;
            }
        }
    }

    private static bool IsBaseEntityType(Type entityType)
    {
        var current = entityType;
        while (current is not null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(BaseEntity<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static void SetProperty(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        property.SetValue(entity, value);
    }
}
