using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// Uygulama başlatma sırasında lisans doğrulama ve middleware ekleme extension'ları.
/// </summary>
public static class LicenseApplicationBuilderExtensions
{
    /// <summary>
    /// Lisans guard middleware'ini pipeline'a ekler.
    /// Her HTTP isteğinde lisans durumunu kontrol eder.
    ///
    /// Kullanım:
    ///   app.UseTodLicenseGuard();
    /// </summary>
    public static IApplicationBuilder UseTodLicenseGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<LicenseGuardMiddleware>();
    }

    /// <summary>
    /// Uygulama baslatilirken lisansi dogrular.
    /// throwOnFailure=true ise gecersiz lisansta uygulama baslamaz.
    /// throwOnFailure=false ise uyari loglar ama baslatir (upload akisi icin).
    ///
    /// Kullanim:
    ///   await app.ValidateLicenseOnStartupAsync();               // uyari modu
    ///   await app.ValidateLicenseOnStartupAsync(throwOnFailure: true);  // zorunlu mod
    /// </summary>
    public static async Task ValidateLicenseOnStartupAsync(
        this WebApplication app,
        bool throwOnFailure = false)
    {
        var logger = app.Services.GetRequiredService<ILogger<LicenseGuardMiddleware>>();
        var licenseService = app.Services.GetRequiredService<ILicenseService>();

        logger.LogInformation("Startup lisans dogrulamasi baslatiliyor...");

        var result = await licenseService.GetCurrentStatusAsync();

        if (!result.IsValid)
        {
            var errors = string.Join(Environment.NewLine, result.Errors);

            if (throwOnFailure)
            {
                logger.LogCritical("Lisans dogrulamasi basarisiz! Uygulama baslatilAMIYOR.\n{Errors}", errors);
                throw new LicenseException(
                    "Uygulama baslatilAMIYOR: Gecerli bir lisans bulunamadi.",
                    result.Errors);
            }

            logger.LogWarning(
                "Lisans dogrulamasi basarisiz. Uygulama baslatiliyor ama lisans yuklenene kadar " +
                "API istekleri engellenecek.\n{Errors}", errors);
            return;
        }

        logger.LogInformation(
            "Lisans dogrulandi. Musteri: {Customer}, Son kullanma: {Expires}",
            result.License!.CustomerName,
            result.License.ExpiresAtUtc.ToString("yyyy-MM-dd"));
    }
}
