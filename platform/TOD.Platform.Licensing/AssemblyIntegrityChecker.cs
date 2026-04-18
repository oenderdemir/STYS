using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Lisanslama assembly'lerinin hash'ini kontrol eder.
///
/// Binary patch saldırılarını tamamen önleyemez ama maliyetini artırır.
/// Saldırganın hem assembly'i hem bu hash kontrolünü patch etmesi gerekir.
///
/// Sınırlamalar:
/// - Saldırgan bu sınıfı da patch edebilir (return true yapabilir).
/// - Bu yüzden bu kontrol, birden fazla katmanda tekrarlanmalıdır.
/// - İdeal olarak hash değerleri build pipeline'da otomatik inject edilmelidir.
/// </summary>
public sealed class AssemblyIntegrityChecker : Abstractions.IAssemblyIntegrityChecker
{
    // Build sırasında güncellenmesi gereken beklenen hash değerleri.
    // Key: assembly dosya adı, Value: SHA256 hash (Base64).
    // İlk kurulumda boş bırakılabilir — build script ile doldurulur.
    private static readonly Dictionary<string, string> ExpectedHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Örnek: ["TOD.Platform.Licensing.dll"] = "abc123..."
        // Bu değerler CI/CD pipeline'da build sonrası inject edilmelidir.
    };

    private readonly LicensingOptions _options;

    public AssemblyIntegrityChecker(IOptions<LicensingOptions> options)
    {
        _options = options.Value;
    }

    public bool IsIntact()
    {
        var effectiveHashes = BuildEffectiveHashSet();

        if (effectiveHashes.Count == 0)
        {
            // Production'da hash gereksinimi aktifse boş listeyi hata kabul et.
            if (_options.RequireIntegrityHashesInProduction && IsProductionEnvironment())
                return false;

            return true;
        }

        foreach (var (assemblyName, expectedHash) in effectiveHashes)
        {
            var assemblyPath = Path.Combine(AppContext.BaseDirectory, assemblyName);

            if (!File.Exists(assemblyPath))
                return false;

            try
            {
                var fileBytes = File.ReadAllBytes(assemblyPath);
                var actualHash = Convert.ToBase64String(SHA256.HashData(fileBytes));

                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private Dictionary<string, string> BuildEffectiveHashSet()
    {
        var effective = new Dictionary<string, string>(ExpectedHashes, StringComparer.OrdinalIgnoreCase);
        foreach (var (assemblyName, expectedHash) in _options.IntegrityHashes)
        {
            if (string.IsNullOrWhiteSpace(assemblyName) || string.IsNullOrWhiteSpace(expectedHash))
                continue;

            effective[assemblyName.Trim()] = expectedHash.Trim();
        }

        return effective;
    }

    private static bool IsProductionEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? string.Empty;
        return environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
    }
}
