using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TOD.Platform.Licensing.Abstractions;

namespace Tod.LicenseGenerator;

internal static class LicenseGeneratorCore
{
    public const string PrivateKeyFile = "license-private.key";
    public const string PublicKeyFile = "license-public.key";

    public static async Task<ECDsa> EnsureKeysExistAsync(string privateKeyPath = PrivateKeyFile, string publicKeyPath = PublicKeyFile)
    {
        if (File.Exists(privateKeyPath))
        {
            var existing = await File.ReadAllTextAsync(privateKeyPath);
            var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(existing.Trim()), out _);
            return ecdsa;
        }

        var newEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privateKeyBase64 = Convert.ToBase64String(newEcdsa.ExportPkcs8PrivateKey());
        var publicKeyBase64 = Convert.ToBase64String(newEcdsa.ExportSubjectPublicKeyInfo());

        await File.WriteAllTextAsync(privateKeyPath, privateKeyBase64);
        await File.WriteAllTextAsync(publicKeyPath, publicKeyBase64);
        return newEcdsa;
    }

    public static string ComputeFingerprintHash(
        FingerprintProfile profile,
        string environmentName,
        string instanceId,
        string customerCode,
        string deploymentMarker)
    {
        var sb = new StringBuilder();
        sb.Append("PROFILE:");
        sb.Append(profile.ToString().ToUpperInvariant());
        sb.Append('|');
        sb.Append(environmentName.ToUpperInvariant());
        sb.Append('|');
        sb.Append(instanceId.ToUpperInvariant());
        sb.Append('|');
        sb.Append(customerCode.ToUpperInvariant());
        sb.Append('|');
        sb.Append(deploymentMarker.ToUpperInvariant());

        if (profile == FingerprintProfile.PhysicalServer)
        {
            sb.Append('|');
            sb.Append(Environment.MachineName.ToUpperInvariant());
            sb.Append('|');
            sb.Append(RuntimeInformation.OSDescription.ToUpperInvariant());
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    public static byte[] BuildCanonicalPayload(LicenseDocument license)
    {
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

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Encoding.UTF8.GetBytes(json);
    }

    public static string BuildPublicKeyPartsText(string publicKeyBase64)
    {
        const int chunkSize = 30;
        var lines = new List<string>
        {
            "private static readonly string[] PublicKeyParts =",
            "["
        };

        for (var i = 0; i < publicKeyBase64.Length; i += chunkSize)
        {
            var part = publicKeyBase64[i..Math.Min(i + chunkSize, publicKeyBase64.Length)];
            var comma = i + chunkSize < publicKeyBase64.Length ? "," : string.Empty;
            lines.Add($"    \"{part}\"{comma}");
        }

        lines.Add("];");
        return string.Join(Environment.NewLine, lines);
    }
}

