using Microsoft.EntityFrameworkCore;

namespace STYS.Infrastructure.EntityFramework;

/// <summary>
/// StysAppDbContext'in DI kaydini tek, paylasilan bir yerden yapar - backend/Program.cs VE
/// tests/STYS.Tests/DbContextFactoryDependencyInjectionTests.cs AYNI bu metodu cagirir, boylece
/// test GERCEKTEN production'daki kayit koduyla calisir (kopyalanmis/elle senkronize edilen iki
/// ayri kayit blogu degil).
/// </summary>
public static class StysPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// AddDbContextFactory&lt;StysAppDbContext&gt; EF Core 10'da yalnizca
    /// IDbContextFactory&lt;StysAppDbContext&gt;'i DEGIL, StysAppDbContext'in KENDISINI de TEK bir
    /// Scoped descriptor olarak DI'a kaydeder (bu, tests/STYS.Tests/
    /// DbContextFactoryDependencyInjectionTests.cs'teki descriptor-sayisi testiyle dogrulanmistir -
    /// ayrica bir "AddScoped(sp => factory.CreateDbContext())" satirina GEREK YOKTUR; boyle bir
    /// satir eklemek StysAppDbContext icin IKINCI, cakisan bir DI kaydi olusturur).
    ///
    /// ServiceLifetime.Scoped ACIKCA belirtilir - AddDbContextFactory'nin VARSAYILANI Singleton'dir.
    /// StysAppDbContext'in constructor'i SCOPED ICurrentUserAccessor/ICurrentTenantAccessor alir;
    /// factory Singleton olsaydi, TEK bir factory ornegi TUM uygulama omru boyunca paylasilir ve
    /// CreateDbContext() cagrildiginda bu scoped bagimliliklari KENDI (kok/singleton'a bagli)
    /// provider'indan cozmeye calisirdi - bu ya "Cannot resolve scoped service from root provider"
    /// istisnasiyla (ValidateScopes acikken) patlar, ya da (kapali iken) ilk cozulen kullanicinin/
    /// tenantin degerlerini SESSIZCE tum sonraki istekler icin "captive dependency" olarak
    /// dondurmeye devam ederdi - farkli kullanicilarin/tesislerin birbirinin verisini gormesi gibi
    /// ciddi bir veri sizintisina yol acardi. Scoped lifetime ile, HER HTTP istegi/scope KENDI
    /// factory ornegini (ve dolayisiyla o scope'a ait ICurrentUserAccessor/ICurrentTenantAccessor'i)
    /// alir; CreateDbContext() ile uretilen TUM context'ler (dogrudan enjekte edilen StysAppDbContext
    /// dahil, ve PosTahsilatValorAktarimService'in sayac duzeltmesi/cleanup icin urettigi ek
    /// context'ler) dogru istek/kullanici/tenant baglamini gorur.
    /// </summary>
    public static IServiceCollection AddStysPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<StysAppDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);
        return services;
    }
}
