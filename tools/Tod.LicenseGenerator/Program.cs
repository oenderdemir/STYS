using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TOD.Platform.Licensing.Abstractions;

namespace Tod.LicenseGenerator;

public static class Program
{
    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true
    };

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var command = args.Length > 0 ? args[0] : string.Empty;

        return command switch
        {
            "generate" => await GenerateLicenseAsync(),
            "fingerprint" => ShowFingerprint(),
            "show-public-key" => ShowPublicKey(),
            "gui" => LaunchGui(),
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
              dotnet run -- fingerprint      Bu makinenin fingerprint hash degerini gosterir
              dotnet run -- show-public-key  Public key parcalarini gosterir
              dotnet run -- gui              Kucuk masaustu arayuzunu acar
            """);
        return 0;
    }

    private static int LaunchGui()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LicenseGeneratorForm());
        return 0;
    }

    private static async Task<int> GenerateLicenseAsync()
    {
        using var ecdsa = await LicenseGeneratorCore.EnsureKeysExistAsync();

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
            expiryStr,
            "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow.AddDays(365);

        var modulesInput = Ask("Aktif moduller (virgul ile, bos=tumunu ac)", string.Empty);
        if (!string.IsNullOrEmpty(modulesInput))
        {
            license.EnabledModules = modulesInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var profileInput = Ask("Fingerprint profili (PhysicalServer/Container)", "PhysicalServer");
        var profile = Enum.TryParse<FingerprintProfile>(profileInput, true, out var parsedProfile)
            ? parsedProfile
            : FingerprintProfile.PhysicalServer;
        var deploymentMarker = Ask("Deployment marker (opsiyonel)", string.Empty);

        license.FingerprintHash = LicenseGeneratorCore.ComputeFingerprintHash(
            profile,
            license.EnvironmentName,
            license.InstanceId,
            license.CustomerCode,
            deploymentMarker);

        Console.WriteLine($"\n  Machine Name:   {Environment.MachineName}");
        Console.WriteLine($"  OS:             {RuntimeInformation.OSDescription}");
        Console.WriteLine($"  Fingerprint:    {license.FingerprintHash}");

        var payload = LicenseGeneratorCore.BuildCanonicalPayload(license);
        var signatureBytes = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
        license.Signature = Convert.ToBase64String(signatureBytes);

        var outputPath = $"license-{license.ProductCode}-{license.CustomerCode}.json".ToLowerInvariant();
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(license, PrettyJson));

        Console.WriteLine($"\n--- Lisans uretildi: {outputPath} ---");
        Console.WriteLine($"  License ID:  {license.LicenseId}");
        Console.WriteLine($"  Gecerlilik:  {license.IssuedAtUtc:yyyy-MM-dd HH:mm} -> {license.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine($"  Moduller:    {(license.EnabledModules.Count == 0 ? "Tumunu ac" : string.Join(", ", license.EnabledModules))}");

        return 0;
    }

    private static int ShowFingerprint()
    {
        Console.WriteLine("=== Fingerprint Bilgileri ===\n");
        Console.WriteLine($"  Machine Name:     {Environment.MachineName}");
        Console.WriteLine($"  OS Description:   {RuntimeInformation.OSDescription}");

        var profileInput = Ask("Fingerprint profili (PhysicalServer/Container)", "PhysicalServer");
        var profile = Enum.TryParse<FingerprintProfile>(profileInput, true, out var parsedProfile)
            ? parsedProfile
            : FingerprintProfile.PhysicalServer;
        var env = Ask("Ortam adi", "Production");
        var instanceId = Ask("Instance ID", "instance-01");
        var customerCode = Ask("Musteri kodu");
        var marker = Ask("Deployment marker (opsiyonel)", string.Empty);

        var fp = LicenseGeneratorCore.ComputeFingerprintHash(profile, env, instanceId, customerCode, marker);
        Console.WriteLine($"\n  Fingerprint Hash: {fp}");
        return 0;
    }

    private static int ShowPublicKey()
    {
        if (!File.Exists(LicenseGeneratorCore.PublicKeyFile))
        {
            Console.WriteLine("Public key bulunamadi. Once 'generate' calistirin.");
            return 1;
        }

        var publicKeyBase64 = File.ReadAllText(LicenseGeneratorCore.PublicKeyFile).Trim();
        Console.WriteLine("EcdsaLicenseSignatureVerifier.PublicKeyParts dizisine kopyalayin:\n");
        Console.WriteLine(LicenseGeneratorCore.BuildPublicKeyPartsText(publicKeyBase64));
        return 0;
    }

    private static string Ask(string prompt, string? defaultValue = null)
    {
        if (!string.IsNullOrEmpty(defaultValue))
        {
            Console.Write($"  {prompt} [{defaultValue}]: ");
        }
        else
        {
            Console.Write($"  {prompt}: ");
        }

        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(input) ? (defaultValue ?? string.Empty) : input;
    }
}

