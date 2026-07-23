using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.AccessScope;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.MuhasebeDonemleri.Mapping;
using STYS.Muhasebe.MuhasebeDonemleri.Repositories;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Mapping;
using STYS.Muhasebe.MuhasebeFisleri.Repositories;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.MuhasebeHesapBakiyeleri.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.PosTahsilatValorleri.Services;
using TOD.Platform.AspNetCore.Logging;
using TOD.Platform.Security.Auth.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// backend/Program.cs'te AddDbContext&lt;StysAppDbContext&gt; yerine AddDbContextFactory +
/// AddScoped(sp =&gt; factory.CreateDbContext()) kullanilmasinin GERCEK bir DI hatasi
/// (Singleton-lifetime factory'nin scoped ICurrentUserAccessor/ICurrentTenantAccessor'i kok
/// provider'dan cozmeye calismasi) uretmedigini dogrular. Program.cs'in BIREBIR AYNI iki
/// kayit satiri (bkz. BuildContainer, "=== Program.cs..." yorumu) kullanilir; DbContextFactory
/// disindaki (Redis, kimlik dogrulama, lisanslama, SignalR vb.) kayitlar bu testin kapsami
/// disindadir - onlar bu DI-lifetime hatasiyla ILGISIZDIR ve tam ASP.NET Core host'unu
/// (WebApplicationFactory) ayaga kaldirmak, bu odakli regresyon testi icin gereksiz bir kirilganlik
/// katardi.
/// </summary>
public class DbContextFactoryDependencyInjectionTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");

    private sealed class ScopedCurrentUserAccessor : ICurrentUserAccessor
    {
        public string? UserName { get; set; }
        public string? GetCurrentUserName() => UserName;
        public Guid? GetCurrentUserId() => Guid.NewGuid();
    }

    private sealed class ScopedCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public int? KurumId { get; set; }
        public int? GetCurrentKurumId() => KurumId;
        public IReadOnlyList<int> GetAccessibleKurumIds() => KurumId.HasValue ? [KurumId.Value] : [];
        public bool IsSuperAdmin() => false;
        public bool IsKurumAdmin() => false;
    }

    private sealed class FakeUserAccessScopeService : IUserAccessScopeService
    {
        public Task<DomainAccessScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DomainAccessScope.Unscoped());
    }

    private sealed class FakeMuhasebeTesisScopeService : IMuhasebeTesisScopeService
    {
        public Task<int[]> GetEffectiveTesisIdsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<int>());
        public Task<int[]> GetEffectiveTesisIdsAsync(DomainAccessScope scope, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<int>());
        public Task EnsureCanAccessTesisAsync(int tesisId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeDomainOperationLogger : IDomainOperationLogger
    {
        public void Started(string eventName, object payload) { }
        public void Completed(string eventName, object payload) { }
        public void Warning(string eventName, object payload) { }
        public void Failed(string eventName, Exception exception, object payload) { }
    }

    /// <summary>
    /// backend/Program.cs'teki DbContext/DbContextFactory kayit BLOGUNUN (satir satir) birebir
    /// aynisi - bkz. "AddDbContextFactory + AddScoped(sp => factory.CreateDbContext())" yorumu.
    /// Geri kalan kayitlar (mapper, repo/servis grafigi) PosTahsilatValorAktarimService'i GERCEK
    /// (Fake DEGIL) MuhasebeDonemService/MuhasebeFisService implementasyonlariyla cozebilmek
    /// icindir - yalnizca IUserAccessScopeService/IMuhasebeTesisScopeService/IDomainOperationLogger
    /// gibi HTTP-context/auth altyapisina bagli servisler (bu testin odaginin disinda) Fake'tir.
    /// </summary>
    private static ServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddScoped<ScopedCurrentUserAccessor>();
        services.AddScoped<ICurrentUserAccessor>(sp => sp.GetRequiredService<ScopedCurrentUserAccessor>());
        services.AddScoped<ScopedCurrentTenantAccessor>();
        services.AddScoped<ICurrentTenantAccessor>(sp => sp.GetRequiredService<ScopedCurrentTenantAccessor>());

        // === backend/Program.cs ile BIREBIR AYNI iki satir ===
        services.AddDbContextFactory<StysAppDbContext>(options => options.UseSqlServer(ConnectionString), ServiceLifetime.Scoped);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<StysAppDbContext>>().CreateDbContext());
        // === === ===

        services.AddSingleton(new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<MuhasebeDonemProfile>();
            cfg.AddProfile<MuhasebeFisProfile>();
        }, NullLoggerFactory.Instance));
        services.AddScoped<IMapper>(sp => sp.GetRequiredService<MapperConfiguration>().CreateMapper(sp.GetService));

        services.AddScoped<IUserAccessScopeService, FakeUserAccessScopeService>();
        services.AddScoped<IMuhasebeTesisScopeService, FakeMuhasebeTesisScopeService>();
        services.AddScoped<IDomainOperationLogger, FakeDomainOperationLogger>();

        services.AddScoped(sp => new MuhasebeDonemRepository(sp.GetRequiredService<StysAppDbContext>(), sp.GetRequiredService<IMapper>()));
        services.AddScoped<IMuhasebeDonemService>(sp => new MuhasebeDonemService(
            sp.GetRequiredService<MuhasebeDonemRepository>(), sp.GetRequiredService<IMapper>(),
            sp.GetRequiredService<StysAppDbContext>(), sp.GetRequiredService<IMuhasebeTesisScopeService>()));

        services.AddScoped(sp => new MuhasebeFisRepository(sp.GetRequiredService<StysAppDbContext>(), sp.GetRequiredService<IMapper>()));
        services.AddScoped(sp => new MuhasebeHesapBakiyeGuncellemeService(sp.GetRequiredService<StysAppDbContext>()));
        services.AddScoped<IMuhasebeFisService>(sp => new MuhasebeFisService(
            sp.GetRequiredService<MuhasebeFisRepository>(), sp.GetRequiredService<IMapper>(),
            sp.GetRequiredService<StysAppDbContext>(), sp.GetRequiredService<IMuhasebeDonemService>(),
            sp.GetRequiredService<MuhasebeHesapBakiyeGuncellemeService>(), sp.GetRequiredService<IUserAccessScopeService>(),
            sp.GetRequiredService<IDomainOperationLogger>()));

        services.AddScoped<PosTahsilatValorAktarimService>();

        // ValidateScopes: kok provider'dan scoped servis cozme girisimlerini (tam da bu bulgunun
        // bahsettigi hatayi) ISTISNA firlatarak yakalar. ValidateOnBuild: eksik/yanlis kayitlari
        // BuildServiceProvider aninda tespit eder.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [IntegrationFact]
    public async Task ScopedFactory_TekScopeIcinde_StysAppDbContextVePosTahsilatValorAktarimServiceCozulur()
    {
        await using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        // 1) Dogrudan enjekte edilen StysAppDbContext cozulebilmeli.
        var dbContext = scope.ServiceProvider.GetRequiredService<StysAppDbContext>();
        Assert.NotNull(dbContext);

        // 2) IDbContextFactory ile IKINCI, BAGIMSIZ bir context olusturulabilmeli (farkli bir
        // StysAppDbContext ornegi - PosTahsilatValorAktarimService'in sayac duzeltmesi/cleanup
        // icin kullandigi desenin AYNISI).
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<StysAppDbContext>>();
        await using var ikinciContext = await factory.CreateDbContextAsync();
        Assert.NotNull(ikinciContext);
        Assert.NotSame(dbContext, ikinciContext);

        // 3) PosTahsilatValorAktarimService (gercek uretim sinifi) bu scope icinde cozulebilmeli -
        // Singleton-lifetime bir factory olsaydi, bu noktada (veya CreateDbContext cagrisinda)
        // "Cannot resolve scoped service from root provider" istisnasi alinirdi.
        var aktarimService = scope.ServiceProvider.GetRequiredService<PosTahsilatValorAktarimService>();
        Assert.NotNull(aktarimService);
    }

    [IntegrationFact]
    public async Task ScopedFactory_FarkliScopeVeTenantlar_VeriSizintisiOlmadanDogruBaglamiGorur()
    {
        await using var provider = BuildContainer();

        // Scope A: kullanici "userA", tenant 100.
        using var scopeA = provider.CreateScope();
        scopeA.ServiceProvider.GetRequiredService<ScopedCurrentUserAccessor>().UserName = "userA";
        scopeA.ServiceProvider.GetRequiredService<ScopedCurrentTenantAccessor>().KurumId = 100;
        var ctxA1 = scopeA.ServiceProvider.GetRequiredService<StysAppDbContext>();
        var factoryA = scopeA.ServiceProvider.GetRequiredService<IDbContextFactory<StysAppDbContext>>();
        await using var ctxA2 = await factoryA.CreateDbContextAsync();

        // Scope B: kullanici "userB", tenant 200 - AYNI container/factory kayitlari kullanilir.
        using var scopeB = provider.CreateScope();
        scopeB.ServiceProvider.GetRequiredService<ScopedCurrentUserAccessor>().UserName = "userB";
        scopeB.ServiceProvider.GetRequiredService<ScopedCurrentTenantAccessor>().KurumId = 200;
        var ctxB1 = scopeB.ServiceProvider.GetRequiredService<StysAppDbContext>();
        var factoryB = scopeB.ServiceProvider.GetRequiredService<IDbContextFactory<StysAppDbContext>>();
        await using var ctxB2 = await factoryB.CreateDbContextAsync();

        // Ayni scope icindeki HER IKI context de (dogrudan enjekte edilen VE factory ile uretilen)
        // O scope'un tenant'ini gormeli - scope'lar arasinda veri SIZINTISI OLMAMALI.
        Assert.Equal(100, ctxA1.CurrentKurumId);
        Assert.Equal(100, ctxA2.CurrentKurumId);
        Assert.Equal(200, ctxB1.CurrentKurumId);
        Assert.Equal(200, ctxB2.CurrentKurumId);

        // SaveChanges audit CreatedBy/UpdatedBy dogru KULLANICIDAN gelmeli - scope A'da eklenen
        // bir kayit "userA", scope B'de eklenen "userB" olarak damgalanmali.
        var maliYilA = 1900 + Math.Abs(Guid.NewGuid().GetHashCode() % 90);
        var maliYilB = 1900 + Math.Abs(Guid.NewGuid().GetHashCode() % 90) + 100;
        ctxA1.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = -100001, MaliYil = maliYilA, SonNumara = 1 });
        await ctxA1.SaveChangesAsync();
        ctxB1.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = -100002, MaliYil = maliYilB, SonNumara = 1 });
        await ctxB1.SaveChangesAsync();

        await using var verifyContext = new StysAppDbContext(
            new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options);
        var sayacA = await verifyContext.PosValorFisNoSayaclari.SingleAsync(x => x.TesisId == -100001);
        var sayacB = await verifyContext.PosValorFisNoSayaclari.SingleAsync(x => x.TesisId == -100002);
        Assert.Equal("userA", sayacA.CreatedBy);
        Assert.Equal("userB", sayacB.CreatedBy);

        // Temizlik.
        await verifyContext.PosValorFisNoSayaclari.Where(x => x.TesisId == -100001 || x.TesisId == -100002).ExecuteDeleteAsync();
    }
}
