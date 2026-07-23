using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
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
using TOD.Platform.Security;
using TOD.Platform.Security.Auth.Services;
using Xunit;

namespace STYS.Tests;

/// <summary>
/// backend/Program.cs'in StysAppDbContext DI kaydini (AddStysPersistence — bkz.
/// backend/Infrastructure/EntityFramework/StysPersistenceServiceCollectionExtensions.cs) GERCEKTEN
/// dogru kurdugunu dogrular. Onceki tur (commit 988b427), "AddDbContextFactory yalnizca
/// IDbContextFactory'yi kaydeder, StysAppDbContext'i AYRICA kaydetmez" varsayimiyla ekstra bir
/// AddScoped(sp => factory.CreateDbContext()) satiri ekliyordu — bu, EF Core 10'un GERCEK
/// davranisina AYKIRIYDI: AddDbContextFactory context tipini de (Scoped) kaydeder, dolayisiyla o
/// ekstra satir StysAppDbContext icin IKINCI, cakisan bir DI kaydi olusturuyordu. Bu dosyadaki
/// ilk test (StysAppDbContextTekBirDescriptorOlarakKayitli) bunu KANITLAR: gercek EF Core 10
/// paketine karsi descriptor sayisini ve IEnumerable&lt;StysAppDbContext&gt; cozumlemesinin tek
/// eleman urettigini dogrudan kontrol eder.
///
/// TEST ILE PRODUCTION AYNI KOD YOLUNU KULLANIR: hem backend/Program.cs hem bu dosya
/// `AddStysPersistence` uzerinden AYNI kayit metodunu cagirir (kopyalanmis/elle senkronize edilen
/// iki ayri kayit blogu DEGIL). ICurrentUserAccessor/ICurrentTenantAccessor de production'daki
/// GERCEK implementasyonlardir (`TOD.Platform.Security.AddTodPlatformSecurity` ->
/// HttpContextCurrentUserAccessor/HttpContextCurrentTenantAccessor) — Fake DEGIL; "iki farkli HTTP/
/// request scope" her scope'un kendi `IHttpContextAccessor.HttpContext`'ine farkli claim'ler
/// (kullanici/tenant) atanarak simule edilir.
///
/// Tam ASP.NET Core host'unu (WebApplicationFactory&lt;Program&gt; — Redis, JWT auth, lisanslama,
/// SignalR dahil) ayaga kaldirmak yerine bu odakli DI-container testi tercih edildi: raporlanan
/// hata SPESIFIK olarak StysAppDbContext'in DI yasam dongusuyle ilgiliydi, tam host'u ayaga
/// kaldirmak bu regresyon testine gereksiz kirilganlik (Redis/lisans/SignalR bagimliliklari)
/// katardi. Gercek uygulamanin da (host dahil) hatasiz calistigi AYRICA, gercek `dotnet run` ile
/// manuel olarak dogrulandi — bkz. docs/pos-tahsilat-valor-takip-uygulama-raporu.md "Güncelleme 6"
/// bolumundeki komutlar ve ciktilar.
/// </summary>
public class DbContextFactoryDependencyInjectionTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("STYS_INTEGRATION_TEST_CONNECTION_STRING");

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
    /// backend/Program.cs'in AYNI `AddStysPersistence` uzantı metodunu cagirir (kopya DEGIL).
    /// ICurrentUserAccessor/ICurrentTenantAccessor icin `TOD.Platform.Security.AddTodPlatformSecurity`
    /// ile GERCEK (Fake degil) HttpContextCurrentUserAccessor/HttpContextCurrentTenantAccessor
    /// kaydedilir. Geri kalan kayitlar (mapper, repo/servis grafigi) PosTahsilatValorAktarimService'i
    /// GERCEK MuhasebeDonemService/MuhasebeFisService implementasyonlariyla cozebilmek icindir -
    /// yalnizca IUserAccessScopeService/IMuhasebeTesisScopeService/IDomainOperationLogger gibi
    /// yetkilendirme-disi, bu testin odaginin disindaki servisler Fake'tir.
    /// </summary>
    private static ServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Gercek uretim kodu - Program.cs ile AYNI metot.
        services.AddStysPersistence(ConnectionString!);

        // Gercek uretim kodu - ICurrentUserAccessor/ICurrentTenantAccessor'in GERCEK,
        // IHttpContextAccessor tabanli implementasyonlari (Fake degil).
        services.AddTodPlatformSecurity();

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

        // ValidateScopes: kok provider'dan scoped servis cozme girisimlerini (raporlanan orijinal
        // hatayi) ISTISNA firlatarak yakalar. ValidateOnBuild: eksik/yanlis kayitlari
        // BuildServiceProvider aninda tespit eder.
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>Bir scope'un IHttpContextAccessor'ina, verilen kullanici/tenant claim'lerini tasiyan
    /// sahte bir HttpContext atar - HttpContextCurrentUserAccessor/HttpContextCurrentTenantAccessor
    /// (GERCEK production sinifi) bu context'ten okur. Bu, gercek bir HTTP istegi olmadan "bu
    /// scope su kullanici/tenant'a ait bir istegi temsil ediyor" durumunu dogru sekilde simule
    /// etmenin GERCEK-KOD-YOLU uzerinden yoludur (accessor sinifini FAKE'lemek yerine).</summary>
    private static void SimulateHttpRequestForScope(IServiceProvider scopedProvider, string userId, string userName, int kurumId)
    {
        var httpContextAccessor = scopedProvider.GetRequiredService<IHttpContextAccessor>();
        var claims = new List<Claim>
        {
            new("userId", userId),
            new(ClaimTypes.NameIdentifier, userName),
            new("userName", userName),
            new("kurumId", kurumId.ToString())
        };
        httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
    }

    [IntegrationFact]
    public void StysAppDbContext_TekBirDescriptorOlarakKayitli_IEnumerableIkiContextUretmez()
    {
        var services = new ServiceCollection();
        services.AddStysPersistence(ConnectionString!);

        // EF Core 10'da AddDbContextFactory<TContext> hem IDbContextFactory<TContext>'i HEM DE
        // TContext'in KENDISINI (Scoped) kaydeder. AddStysPersistence bunun UZERINE AYRICA bir
        // AddScoped<StysAppDbContext> eklemez - eklerse, IServiceCollection'da StysAppDbContext
        // icin IKI descriptor olusur (cakisan/mukerrer kayit).
        var descriptors = services.Where(d => d.ServiceType == typeof(StysAppDbContext)).ToList();
        Assert.Single(descriptors);
        Assert.Equal(ServiceLifetime.Scoped, descriptors[0].Lifetime);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        // IEnumerable<StysAppDbContext> (tum kayitlari dondurur) TAM OLARAK bir eleman uretmeli -
        // cakisan bir ikinci kayit olsaydi burada IKI FARKLI StysAppDbContext ornegi donerdi.
        var hepsi = scope.ServiceProvider.GetServices<StysAppDbContext>().ToList();
        Assert.Single(hepsi);
    }

    [IntegrationFact]
    public async Task ScopedFactory_TekScopeIcinde_StysAppDbContextVePosTahsilatValorAktarimServiceCozulur()
    {
        await using var provider = BuildContainer();
        using var scope = provider.CreateScope();
        SimulateHttpRequestForScope(scope.ServiceProvider, Guid.NewGuid().ToString(), "userA", 100);

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

        // 4) Gercek ICurrentUserAccessor/ICurrentTenantAccessor bu scope'a atanan claim'leri
        // dogru okumali (accessor'lar da GERCEK sinif - HttpContextCurrentUserAccessor/
        // HttpContextCurrentTenantAccessor).
        Assert.Equal("userA", scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>().GetCurrentUserName());
        Assert.Equal(100, scope.ServiceProvider.GetRequiredService<ICurrentTenantAccessor>().GetCurrentKurumId());
        Assert.Equal(100, dbContext.CurrentKurumId);
    }

    [IntegrationFact]
    public async Task ScopedFactory_FarkliHttpScopeVeTenantlar_VeriSizintisiOlmadanDogruBaglamiGorur()
    {
        await using var provider = BuildContainer();

        // ONEMLI: IHttpContextAccessor, ASP.NET Core'da singleton olarak kaydedilir (AddHttpContextAccessor)
        // ve HttpContext'i AsyncLocal ile tasir - GERCEK bir uygulamada her istek KENDI ayri async
        // akisinda (Kestrel'in her baglanti/istek icin actigi ayri cagirim zincirinde) calistigi
        // icin bu dogal olarak izole olur. Bu testte iki "istegi" AYNI thread/async akisinda ART
        // ARDA simule ediyoruz - bu yuzden scope B'nin HttpContext'ini ayarlamadan ONCE scope A'nin
        // TUM okuma/yazma islemlerini (context cozumleme + SaveChanges + dogrulama) TAMAMLIYORUZ.
        // Aksi halde (iki scope'u once kurup sonra ikisini de okumaya calissaydik) AsyncLocal'in
        // paylasilan dogasi nedeniyle ikinci atama BIRINCININ okumasini da etkilerdi - bu, testin
        // kendi yapisindan kaynaklanan bir artefakt olur, gercek isteklerde YASANMAZ (her istek
        // kendi izole ExecutionContext'inde calisir).
        var maliYilA = 1900 + Math.Abs(Guid.NewGuid().GetHashCode() % 90);
        int sayacIdA;
        using (var scopeA = provider.CreateScope())
        {
            SimulateHttpRequestForScope(scopeA.ServiceProvider, Guid.NewGuid().ToString(), "userA", 100);
            var ctxA1 = scopeA.ServiceProvider.GetRequiredService<StysAppDbContext>();
            var factoryA = scopeA.ServiceProvider.GetRequiredService<IDbContextFactory<StysAppDbContext>>();
            await using var ctxA2 = await factoryA.CreateDbContextAsync();

            // Dogrudan enjekte edilen VE factory ile uretilen context, AYNI scope/istek icinde
            // AYNI tenant'i gormeli.
            Assert.Equal(100, ctxA1.CurrentKurumId);
            Assert.Equal(100, ctxA2.CurrentKurumId);

            sayacIdA = -100003 - Random.Shared.Next(0, 100000);
            ctxA1.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = sayacIdA, MaliYil = maliYilA, SonNumara = 1 });
            await ctxA1.SaveChangesAsync();
        }

        var maliYilB = 1900 + Math.Abs(Guid.NewGuid().GetHashCode() % 90) + 100;
        int sayacIdB;
        using (var scopeB = provider.CreateScope())
        {
            SimulateHttpRequestForScope(scopeB.ServiceProvider, Guid.NewGuid().ToString(), "userB", 200);
            var ctxB1 = scopeB.ServiceProvider.GetRequiredService<StysAppDbContext>();
            var factoryB = scopeB.ServiceProvider.GetRequiredService<IDbContextFactory<StysAppDbContext>>();
            await using var ctxB2 = await factoryB.CreateDbContextAsync();

            // Scope B, scope A'nin ARTIK KAPANMIS olmasina ragmen KENDI (200) tenant'ini gormeli -
            // scope A'nin verisinden/baglamindan HICBIR SIZINTI olmamali.
            Assert.Equal(200, ctxB1.CurrentKurumId);
            Assert.Equal(200, ctxB2.CurrentKurumId);

            sayacIdB = -100004 - Random.Shared.Next(0, 100000);
            ctxB1.PosValorFisNoSayaclari.Add(new PosValorFisNoSayac { TesisId = sayacIdB, MaliYil = maliYilB, SonNumara = 1 });
            await ctxB1.SaveChangesAsync();
        }

        // SaveChanges audit CreatedBy/UpdatedBy dogru KULLANICIDAN gelmeli - scope A'da eklenen
        // kayit "userA", scope B'de eklenen "userB" olarak damgalanmali.
        await using var verifyContext = new StysAppDbContext(
            new DbContextOptionsBuilder<StysAppDbContext>().UseSqlServer(ConnectionString).Options);
        var sayacA = await verifyContext.PosValorFisNoSayaclari.SingleAsync(x => x.TesisId == sayacIdA);
        var sayacB = await verifyContext.PosValorFisNoSayaclari.SingleAsync(x => x.TesisId == sayacIdB);
        Assert.Equal("userA", sayacA.CreatedBy);
        Assert.Equal("userB", sayacB.CreatedBy);

        // Temizlik.
        await verifyContext.PosValorFisNoSayaclari.Where(x => x.TesisId == sayacIdA || x.TesisId == sayacIdB).ExecuteDeleteAsync();
    }
}
