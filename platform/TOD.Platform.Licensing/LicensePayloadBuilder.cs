using System.Text;
using System.Text.Json;
using TOD.Platform.Licensing.Abstractions;

namespace TOD.Platform.Licensing;

/// <summary>
/// Lisans belgesinden imzalanacak canonical payload üretir.
/// Signature alanı hariç tüm alanlar deterministik sırayla JSON'a serialize edilir.
/// Hem imzalama (generator) hem doğrulama (runtime) tarafında aynı çıktıyı üretir.
/// </summary>
public static class LicensePayloadBuilder
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        // Deterministik çıktı için sıralı ve compact JSON
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Signature hariç tüm alanlardan canonical byte[] üretir.
    /// Alanlar alfabetik sırayla bir dictionary'e konur, böylece JSON çıktısı deterministik olur.
    /// </summary>
    public static byte[] BuildCanonicalPayload(LicenseDocument license)
    {
        // Alanlar alfabetik sıraya göre eklenir — her iki tarafta da aynı sıra garanti edilir.
        var payload = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["customerCode"] = license.CustomerCode,
            ["customerName"] = license.CustomerName,
            ["enabledModules"] = license.EnabledModules.OrderBy(m => m, StringComparer.Ordinal).ToList(),
            ["environmentName"] = license.EnvironmentName,
            ["expiresAtUtc"] = license.ExpiresAtUtc.ToUniversalTime().ToString("O"),
            ["fingerprintHash"] = license.FingerprintHash,
            ["instanceId"] = license.InstanceId,
            ["issuedAtUtc"] = license.IssuedAtUtc.ToUniversalTime().ToString("O"),
            ["licenseId"] = license.LicenseId,
            ["licenseVersion"] = license.LicenseVersion,
            ["productCode"] = license.ProductCode
        };

        var json = JsonSerializer.Serialize(payload, CanonicalOptions);
        return Encoding.UTF8.GetBytes(json);
    }
}
