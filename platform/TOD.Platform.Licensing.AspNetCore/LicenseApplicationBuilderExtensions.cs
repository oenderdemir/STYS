using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// Uygulama baslatma sirasinda lisans dogrulama ve middleware ekleme extension'lari.
/// </summary>
public static class LicenseApplicationBuilderExtensions
{
    /// <summary>
    /// Lisans guard middleware'ini pipeline'a ekler.
    /// Her HTTP isteginde lisans durumunu kontrol eder.
    ///
    /// Kullanim:
    ///   app.UseTodLicenseGuard();
    /// </summary>
    public static IApplicationBuilder UseTodLicenseGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LicenseGuardMiddleware>();
    }

    /// <summary>
    /// Uygulama baslatilirken lisansi dogrular.
    ///
    /// Davranis:
    /// - Production ortaminda (veya <paramref name="throwOnFailure"/> acik ise): gecersiz lisansta uygulama baslatmaz.
    /// - Development/Staging'de: uyari loglar, devam eder (upload/ilk kurulum akisi).
    ///
    /// Kullanim:
    ///   await app.ValidateLicenseOnStartupAsync();                      // env-aware (Production'da fail-fast)
    ///   await app.ValidateLicenseOnStartupAsync(throwOnFailure: true);  // her ortamda zorla fail-fast
    ///   await app.ValidateLicenseOnStartupAsync(throwOnFailure: false); // her ortamda sadece uyari
    /// </summary>
    public static async Task ValidateLicenseOnStartupAsync(
        this WebApplication app,
        bool? throwOnFailure = null)
    {
        var logger = app.Services.GetRequiredService<ILogger<LicenseGuardMiddleware>>();
        var licenseService = app.Services.GetRequiredService<ILicenseService>();
        var env = app.Services.GetRequiredService<IWebHostEnvironment>();

        // Caller throwOnFailure belirtmediyse ortamdan karar ver: Production -> true, diger -> false
        var effectiveThrow = throwOnFailure ?? env.IsProduction();

        logger.LogInformation(
            "Startup lisans dogrulamasi baslatiliyor... Environment={Env}, FailFast={Fail}",
            env.EnvironmentName, effectiveThrow);

        var result = await licenseService.GetCurrentStatusAsync();

        if (!result.IsValid)
        {
            var errors = string.Join(Environment.NewLine, result.Errors);

            if (effectiveThrow)
            {
                logger.LogCritical(
                    "Lisans dogrulamasi basarisiz! Uygulama baslatilAMIYOR.{NL}{Errors}",
                    Environment.NewLine, errors);
                throw new LicenseException(
                    "Uygulama baslatilAMIYOR: Gecerli bir lisans bulunamadi.",
                    result.Errors);
            }

            logger.LogWarning(
                "Lisans dogrulamasi basarisiz. Development/Staging modunda uygulama baslatiliyor; " +
                "API istekleri lisans yuklenene kadar engellenecek.{NL}{Errors}",
                Environment.NewLine, errors);
            return;
        }

        logger.LogInformation(
            "Lisans dogrulandi. Musteri: {Customer}, Son kullanma: {Expires}",
            result.License!.CustomerName,
            result.License.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
    }
}
