using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Iller.Entities;
using STYS.Iller.Repositories;
using STYS.Kurumlar.Entities;
using STYS.Tesisler.Entities;
using STYS.Tesisler.Mapping;
using STYS.Tesisler.Repositories;
using STYS.Tesisler.Services;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Repositories;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Repositories;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Persistence.Rdbms.Entities;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class TesisServiceTests
{
    [Fact]
    public async Task GetAllAsync_AktifKurumSuperAdminIcindeSadeceSeciliKurumunTesisleriniDonderir()
    {
        var tenant = new MutableCurrentTenantAccessor
        {
            IsSuperAdminValue = true,
            CurrentKurumIdValue = null
        };
        await using var dbContext = CreateDbContext(tenant);
        await SeedTesislerAsync(dbContext);
        tenant.CurrentKurumIdValue = 100;

        var service = CreateService(dbContext, tenant, DomainAccessScope.Unscoped());

        var tesisler = (await service.GetAllAsync()).ToList();

        Assert.Single(tesisler);
        Assert.All(tesisler, tesis => Assert.Equal(100, tesis.KurumId));
        Assert.Equal("TRT Trabzon Misafirhane", tesisler[0].Ad);
    }

    [Fact]
    public async Task GetAllAsync_AktifKurumDegisikligindeIkinciKurumunTesisleriniDonderir()
    {
        var tenant = new MutableCurrentTenantAccessor
        {
            IsSuperAdminValue = true,
            CurrentKurumIdValue = null
        };
        await using var dbContext = CreateDbContext(tenant);
        await SeedTesislerAsync(dbContext);
        tenant.CurrentKurumIdValue = 200;

        var service = CreateService(dbContext, tenant, DomainAccessScope.Unscoped());

        var tesisler = (await service.GetAllAsync()).ToList();

        Assert.Single(tesisler);
        Assert.All(tesisler, tesis => Assert.Equal(200, tesis.KurumId));
        Assert.Equal("Ankara Misafirhane", tesisler[0].Ad);
    }

    [Fact]
    public async Task GetAllAsync_AktifKurumYoksaScopedKullaniciBosListeAlir()
    {
        var tenant = new MutableCurrentTenantAccessor
        {
            IsSuperAdminValue = true,
            CurrentKurumIdValue = null
        };
        await using var dbContext = CreateDbContext(tenant);
        await SeedTesislerAsync(dbContext);
        tenant.IsSuperAdminValue = false;
        tenant.CurrentKurumIdValue = null;

        var service = CreateService(dbContext, tenant, DomainAccessScope.Scoped([], [], []));

        var tesisler = (await service.GetAllAsync()).ToList();

        Assert.Empty(tesisler);
    }

    [Fact]
    public async Task DeleteAsync_AktifKurumDisindakiTesisiReddeder()
    {
        var tenant = new MutableCurrentTenantAccessor
        {
            IsSuperAdminValue = true,
            CurrentKurumIdValue = null
        };
        await using var dbContext = CreateDbContext(tenant);
        await SeedTesislerAsync(dbContext);
        tenant.CurrentKurumIdValue = 100;

        var service = CreateService(dbContext, tenant, DomainAccessScope.Unscoped());

        var ex = await Assert.ThrowsAsync<BaseException>(() => service.DeleteAsync(200));

        Assert.Equal(403, ex.ErrorCode);
        Assert.Equal("Bu tesis aktif kuruma ait degil.", ex.Message);
    }

    private static StysAppDbContext CreateDbContext(MutableCurrentTenantAccessor tenantAccessor)
    {
        var options = new DbContextOptionsBuilder<StysAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new StysAppDbContext(options, new FakeCurrentUserAccessor(), tenantAccessor);
    }

    private static TesisService CreateService(
        StysAppDbContext dbContext,
        MutableCurrentTenantAccessor tenantAccessor,
        DomainAccessScope scope)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<TesisProfile>();
        }, NullLoggerFactory.Instance);

        var mapper = mapperConfig.CreateMapper();
        var tesisRepository = new TesisRepository(dbContext, mapper);
        var tesisYoneticiRepository = new TesisYoneticiRepository(dbContext, mapper);
        var tesisResepsiyonistRepository = new TesisResepsiyonistRepository(dbContext, mapper);
        var tesisMuhasebeciRepository = new TesisMuhasebeciRepository(dbContext, mapper);
        var ilRepository = new IlRepository(dbContext, mapper);
        var identityOptions = new DbContextOptionsBuilder<TodIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var identityDbContext = new TodIdentityDbContext(identityOptions);
        var userRepository = new UserRepository(identityDbContext, mapper);

        return new TesisService(
            tesisRepository,
            tesisYoneticiRepository,
            tesisResepsiyonistRepository,
            tesisMuhasebeciRepository,
            ilRepository,
            userRepository,
            new FakeUserService(),
            identityDbContext,
            dbContext,
            new FakeUserAccessScopeService(scope),
            new FakeCurrentUserAccessor(),
            tenantAccessor,
            mapper);
    }

    private static async Task SeedTesislerAsync(StysAppDbContext dbContext)
    {
        dbContext.Kurumlar.AddRange(
            new Kurum
            {
                Id = 100,
                Kod = "TRT",
                Ad = "TRT",
                AktifMi = true
            },
            new Kurum
            {
                Id = 200,
                Kod = "DIGER",
                Ad = "Diger Kurum",
                AktifMi = true
            });

        dbContext.Iller.Add(new Il
        {
            Id = 1,
            Ad = "Trabzon",
            AktifMi = true
        });

        dbContext.Tesisler.AddRange(
            new Tesis
            {
                Id = 11,
                KurumId = 100,
                IlId = 1,
                Ad = "TRT Trabzon Misafirhane",
                Telefon = "000",
                Adres = "Trabzon",
                AktifMi = true
            },
            new Tesis
            {
                Id = 22,
                KurumId = 200,
                IlId = 1,
                Ad = "Ankara Misafirhane",
                Telefon = "111",
                Adres = "Ankara",
                AktifMi = true
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        private readonly DomainAccessScope _scope;

        public FakeUserAccessScopeService(DomainAccessScope scope)
        {
            _scope = scope;
        }

        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_scope);
    }

    private sealed class MutableCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? CurrentKurumIdValue { get; set; }
        public bool IsSuperAdminValue { get; set; }

        public int? GetCurrentKurumId() => CurrentKurumIdValue;

        public IReadOnlyList<int> GetAccessibleKurumIds() => CurrentKurumIdValue.HasValue ? [CurrentKurumIdValue.Value] : [];

        public bool IsSuperAdmin() => IsSuperAdminValue;

        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? GetCurrentUserName() => "test-user";

        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class FakeUserService : IUserService
    {
        public Task<IEnumerable<UserDto>> GetAllAsync(Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<UserDto?> GetByIdAsync(Guid id, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<UserDto>> GetPagedAsync(TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request, System.Linq.Expressions.Expression<Func<User, bool>>? predicate = null, Func<IQueryable<User>, IQueryable<User>>? include = null, Func<IQueryable<User>, IOrderedQueryable<User>>? orderBy = null)
            => throw new NotSupportedException();

        public Task<UserDto> AddAsync(UserDto dto)
            => throw new NotSupportedException();

        public Task<UserDto> UpdateAsync(UserDto dto)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id)
            => throw new NotSupportedException();

        public Task<IEnumerable<UserDto>> WhereAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto)
            => throw new NotSupportedException();
    }
}
