using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
    /// - throwOnFailure verilirse birebir o davranis uygulanir.
    /// - verilmezse ortam+config bazli karar verilir:
    ///   Production: Licensing:FailFastOnStartupInProduction (default=false)
    ///   Diger ortamlar: false
    /// Boylece Production'da "kontrollu kilit" modunda sistem ayakta kalir;
    /// lisans yenileme endpoint'leri acik kalirken business endpoint'leri middleware tarafinda kapanir.
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

        // Caller throwOnFailure belirtmediyse ortam+config bazli karar ver.
        var failFastInProduction = app.Configuration.GetValue<bool>("Licensing:FailFastOnStartupInProduction");
        var effectiveThrow = throwOnFailure ?? (env.IsProduction() && failFastInProduction);

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
                "Lisans dogrulamasi basarisiz. Uygulama kontrollu kilit modunda aciliyor; " +
                "business endpoint'leri lisans yuklenene kadar engellenecek, lisans yenileme endpoint'leri acik kalacak.{NL}{Errors}",
                Environment.NewLine, errors);
            return;
        }

        logger.LogInformation(
            "Lisans dogrulandi. Musteri: {Customer}, Son kullanma: {Expires}",
            result.License!.CustomerName,
            result.License.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'"));
    }
}
