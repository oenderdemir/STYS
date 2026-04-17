using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// DI container'a lisanslama servislerini ekleyen extension method'lar.
/// </summary>
public static class LicenseServiceCollectionExtensions
{
    /// <summary>
    /// TOD Lisanslama altyapısını DI'a ekler.
    ///
    /// Kullanım:
    ///   builder.Services.AddTodLicensing(builder.Configuration);
    /// </summary>
    public static IServiceCollection AddTodLicensing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options bind
        services.Configure<LicensingOptions>(
            configuration.GetSection(LicensingOptions.SectionName));

        // Abstractions → Implementation
        services.AddSingleton<ILicenseReader, FileLicenseReader>();
        services.AddSingleton<ILicenseSignatureVerifier, EcdsaLicenseSignatureVerifier>();
        services.AddSingleton<IRuntimeFingerprintProvider, RuntimeFingerprintProvider>();
        services.AddSingleton<ITimeRollbackGuard, TimeRollbackGuard>();
        services.AddSingleton<IAssemblyIntegrityChecker, AssemblyIntegrityChecker>();
        services.AddSingleton<ILicenseValidator, LicenseValidator>();
        services.AddSingleton<ILicenseService, LicenseService>();

        return services;
    }

    /// <summary>
    /// MVC filter'larına LicensedModuleFilter'ı global olarak ekler.
    ///
    /// Kullanım:
    ///   builder.Services.AddControllers(options => options.AddTodLicenseModuleFilter());
    /// </summary>
    public static void AddTodLicenseModuleFilter(this Microsoft.AspNetCore.Mvc.MvcOptions options)
    {
        options.Filters.Add<LicensedModuleFilter>();
    }
}
