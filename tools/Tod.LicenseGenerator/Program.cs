using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TOD.Platform.Licensing.Abstractions;

namespace Tod.LicenseGenerator;

/// <summary>
/// Offline lisans ureten komut satiri araci.
/// Tek komutla key yoksa olusturur, bilgileri sorar, lisans dosyasi uretir.
/// </summary>
public static class Program
{
    private const string PrivateKeyFile = "license-private.key";
    private const string PublicKeyFile = "license-public.key";

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var command = args.Length > 0 ? args[0] : "";

        return command switch
        {
            "generate" => await GenerateLicense(),
            "fingerprint" => ShowFingerprint(),
            "show-public-key" => ShowPublicKey(),
            _ => ShowUsage()
        };
    }

    private static int ShowUsage()
    {
        Console.WriteLine("""
            TOD License Generator
            =====================
            Kullanim:
              dotnet run -- generate         Lisans dosyasi uretir (key yoksa otomatik olusturur)
              dotnet run -- fingerprint      Bu makinenin fingerprint bilgilerini gosterir
              dotnet run -- show-public-key  Public key parcalarini gosterir (uygulamaya gomme icin)
            """);
        return 0;
    }

    /// <summary>
    /// Ana komut: key yoksa uretir, bilgileri sorar, lisans dosyasi olusturur.
    /// </summary>
    private static async Task<int> GenerateLicense()
    {
        var ecdsa = await EnsureKeysExist();

        Console.WriteLine("=== TOD Lisans Uretici ===\n");

        var license = new LicenseDocument
        {
            LicenseId = Guid.NewGuid().ToString("D"),
            LicenseVersion = 1,
            IssuedAtUtc = DateTimeOffset.UtcNow
        };

        license.ProductCode = Ask("Urun kodu", "STYS");
        license.CustomerCode = Ask("Musteri kodu");
        license.CustomerName = Ask("Musteri adi");
        license.EnvironmentName = Ask("Ortam adi", "Production");
        license.InstanceId = Ask("Instance ID", "instance-01");

        var defaultExpiry = DateTimeOffset.UtcNow.AddDays(365).ToString("yyyy-MM-dd HH:mm");
        var expiryStr = Ask("Bitis tarihi (yyyy-MM-dd HH:mm)", defaultExpiry);
        license.ExpiresAtUtc = DateTimeOffset.TryParseExact(
                expiryStr, "yyyy-MM-dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow.AddDays(365);

        var modulesInput = Ask("Aktif moduller (virgul ile, bos=tumunu ac)", "");
        if (!string.IsNullOrEmpty(modulesInput))
        {
            license.EnabledModules = modulesInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var deploymentMarker = Ask("Deployment marker (opsiyonel)", "");

        // Fingerprint
        license.FingerprintHash = ComputeFingerprintHash(
            license.EnvironmentName,
            license.InstanceId,
            license.CustomerCode,
            deploymentMarker);

        Console.WriteLine($"\n  Machine Name:   {Environment.MachineName}");
        Console.WriteLine($"  OS:             {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  Fingerprint:    {license.FingerprintHash}");

        // Imzala
        var payload = BuildCanonicalPayload(license);
        var signatureBytes = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
        license.Signature = Convert.ToBase64String(signatureBytes);
        ecdsa.Dispose();

        // Dosyaya yaz
        var outputPath = $"license-{license.ProductCode}-{license.CustomerCode}.json".ToLowerInvariant();
        var json = JsonSerializer.Serialize(license, PrettyJson);
        await File.WriteAllTextAsync(outputPath, json);

        Console.WriteLine($"\n--- Lisans uretildi: {outputPath} ---");
        Console.WriteLine($"  License ID:  {license.LicenseId}");
        Console.WriteLine($"  Gecerlilik:  {license.IssuedAtUtc:yyyy-MM-dd HH:mm} -> {license.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine($"  Moduller:    {(license.EnabledModules.Count == 0 ? "Tumunu ac" : string.Join(", ", license.EnabledModules))}");
        Console.WriteLine($"\nBu dosyayi uygulamanin arayuzunden yukleyin.");

        return 0;
    }

    /// <summary>
    /// Key dosyalari yoksa otomatik olusturur, varsa yukler.
    /// </summary>
    private static async Task<ECDsa> EnsureKeysExist()
    {
        if (File.Exists(PrivateKeyFile))
        {
            Console.WriteLine($"Mevcut key kullaniliyor: {PrivateKeyFile}");
            var existing = await File.ReadAllTextAsync(PrivateKeyFile);
            var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(existing.Trim()), out _);
            return ecdsa;
        }

        Console.WriteLine("Key bulunamadi, yeni ECDSA P-256 anahtar cifti uretiliyor...");
        var newEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var privateKeyBase64 = Convert.ToBase64String(newEcdsa.ExportPkcs8PrivateKey());
        var publicKeyBase64 = Convert.ToBase64String(newEcdsa.ExportSubjectPublicKeyInfo());

        await File.WriteAllTextAsync(PrivateKeyFile, privateKeyBase64);
        await File.WriteAllTextAsync(PublicKeyFile, publicKeyBase64);

        Console.WriteLine($"  Private key: {PrivateKeyFile}  (Git'e eklEMEYIN!)");
        Console.WriteLine($"  Public key:  {PublicKeyFile}");
        Console.WriteLine();
        PrintPublicKeyParts(publicKeyBase64);
        Console.WriteLine();

        return newEcdsa;
    }

    /// <summary>Bu makinenin fingerprint bilgilerini gosterir.</summary>
    private static int ShowFingerprint()
    {
        Console.WriteLine("=== Fingerprint Bilgileri ===\n");
        Console.WriteLine($"  Machine Name:     {Environment.MachineName}");
        Console.WriteLine($"  OS Description:   {RuntimeInformation.OSDescription}");

        var env = Ask("Ortam adi", "Production");
        var instanceId = Ask("Instance ID", "instance-01");
        var customerCode = Ask("Musteri kodu");
        var marker = Ask("Deployment marker (opsiyonel)", "");

        var fp = ComputeFingerprintHash(env, instanceId, customerCode, marker);
        Console.WriteLine($"\n  Fingerprint Hash: {fp}");
        return 0;
    }

    /// <summary>Mevcut public key'i parcalanmis olarak gosterir.</summary>
    private static int ShowPublicKey()
    {
        if (!File.Exists(PublicKeyFile))
        {
            Console.WriteLine("Public key bulunamadi. Once 'generate' calistirin.");
            return 1;
        }

        var publicKeyBase64 = File.ReadAllText(PublicKeyFile).Trim();
        Console.WriteLine("EcdsaLicenseSignatureVerifier.PublicKeyParts dizisine kopyalayin:\n");
        PrintPublicKeyParts(publicKeyBase64);
        return 0;
    }

    private static void PrintPublicKeyParts(string publicKeyBase64)
    {
        const int chunkSize = 30;
        Console.WriteLine("private static readonly string[] PublicKeyParts =");
        Console.WriteLine("[");
        for (var i = 0; i < publicKeyBase64.Length; i += chunkSize)
        {
            var part = publicKeyBase64[i..Math.Min(i + chunkSize, publicKeyBase64.Length)];
            var comma = i + chunkSize < publicKeyBase64.Length ? "," : "";
            Console.WriteLine($"    \"{part}\"{comma}");
        }
        Console.WriteLine("];");
    }

    private static string Ask(string prompt, string? defaultValue = null)
    {
        if (!string.IsNullOrEmpty(defaultValue))
            Console.Write($"  {prompt} [{defaultValue}]: ");
        else
            Console.Write($"  {prompt}: ");

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? (defaultValue ?? "") : input;
    }

    private static string ComputeFingerprintHash(
        string environmentName, string instanceId, string customerCode, string deploymentMarker)
    {
        var sb = new StringBuilder();
        sb.Append(environmentName.ToUpperInvariant());
        sb.Append('|');
        sb.Append(instanceId.ToUpperInvariant());
        sb.Append('|');
        sb.Append(Environment.MachineName.ToUpperInvariant());
        sb.Append('|');
        sb.Append(RuntimeInformation.OSDescription.ToUpperInvariant());
        sb.Append('|');
        sb.Append(customerCode.ToUpperInvariant());
        sb.Append('|');
        sb.Append(deploymentMarker.ToUpperInvariant());

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static byte[] BuildCanonicalPayload(LicenseDocument license)
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
}
