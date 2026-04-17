using System.Reflection;
using System.Security.Cryptography;

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

    public bool IsIntact()
    {
        if (ExpectedHashes.Count == 0)
            return true; // Hash'ler henüz konfigüre edilmemişse bypass et

        foreach (var (assemblyName, expectedHash) in ExpectedHashes)
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
}
