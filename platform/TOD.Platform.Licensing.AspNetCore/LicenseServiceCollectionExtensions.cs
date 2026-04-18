using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing.AspNetCore;

/// <summary>
/// DI container'a lisanslama servislerini ekleyen extension method'lar.
/// </summary>
public static class LicenseServiceCollectionExtensions
{
    /// <summary>
    /// TOD Lisanslama altyapisini DI'a ekler.
    ///
    /// Production guvenligi:
    /// <paramref name="environment"/> Production ise ve <see cref="LicensingOptions.AllowPublicKeyOverride"/>
    /// = true ise uygulama baslatilmadan once InvalidOperationException firlatilir.
    /// Bu sayede dis config ile public key override'i Production'da engellenir.
    ///
    /// Kullanim:
    ///   builder.Services.AddTodLicensing(builder.Configuration, builder.Environment);
    /// </summary>
    public static IServiceCollection AddTodLicensing(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(LicensingOptions.SectionName);

        // Options bind
        services.Configure<LicensingOptions>(section);

        // Production guvenlik kontrolu: override config'ini once inceleyip
        // Production'da risk varsa erken fail-fast yap.
        EnsureProductionSafe(section, environment);

        // Abstractions -> Implementation
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
    /// Geriye donuk uyumluluk icin parameterless overload; eski cagri sekliyle kirilma olmasin.
    /// Yeni kod her zaman environment parametreli overload'u kullanmalidir.
    /// </summary>
    [Obsolete("Production guvenligi icin environment parametreli overload kullanin.")]
    public static IServiceCollection AddTodLicensing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LicensingOptions>(configuration.GetSection(LicensingOptions.SectionName));

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
    /// MVC filter'larina LicensedModuleFilter'i global olarak ekler.
    ///
    /// Kullanim:
    ///   builder.Services.AddControllers(options => options.AddTodLicenseModuleFilter());
    /// </summary>
    public static void AddTodLicenseModuleFilter(this Microsoft.AspNetCore.Mvc.MvcOptions options)
    {
        options.Filters.Add<LicensedModuleFilter>();
    }

    private static void EnsureProductionSafe(IConfigurationSection section, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        var allowOverride = section.GetValue<bool>(nameof(LicensingOptions.AllowPublicKeyOverride));
        var overrideValue = section.GetValue<string>(nameof(LicensingOptions.PublicKeyOverride));

        if (allowOverride || !string.IsNullOrWhiteSpace(overrideValue))
        {
            throw new InvalidOperationException(
                "Production ortaminda Licensing:AllowPublicKeyOverride/PublicKeyOverride kullanilamaz. " +
                "Public key yalnizca uygulama binary'sine gomulu olmalidir.");
        }
    }
}
