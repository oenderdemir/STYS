using System.Globalization;
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

    private static readonly JsonSerializerOptions LenientJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var command = args.Length > 0 ? args[0] : string.Empty;

        return command switch
        {
            "generate" => await GenerateLicenseAsync(args.Skip(1).ToArray()),
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
              dotnet run -- generate
              dotnet run -- generate --context-file /work/license-context.json
              dotnet run -- generate --context-json '{"productCode":"STYS",...}'
              dotnet run -- fingerprint
              dotnet run -- show-public-key
              dotnet run -- gui
            """);
        return 0;
    }

    private static int LaunchGui()
    {
#if WINDOWS
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new LicenseGeneratorForm());
        return 0;
#else
        Console.WriteLine("GUI modu yalnizca Windows ortaminda kullanilabilir.");
        return 1;
#endif
    }

    private static async Task<int> GenerateLicenseAsync(string[] args)
    {
        var options = ParseGenerateOptions(args);
        if (options.ShowHelp)
        {
            ShowGenerateUsage();
            return 0;
        }

        using var ecdsa = await LicenseGeneratorCore.EnsureKeysExistAsync();
        var generation = options.HasContext
            ? await BuildLicenseFromContextAsync(options)
            : await BuildInteractiveLicenseAsync();

        var license = generation.License;
        var outputPath = ResolveOutputPath(options.OutputPath, generation.SuggestedOutputPath, license);

        var payload = LicenseGeneratorCore.BuildCanonicalPayload(license);
        var signatureBytes = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
        license.Signature = Convert.ToBase64String(signatureBytes);

        EnsureOutputDirectoryExists(outputPath);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(license, PrettyJson));

        PrintLicenseSummary(license, outputPath);
        return 0;
    }

    private static Task<GeneratedLicense> BuildInteractiveLicenseAsync()
    {
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

        var defaultExpiry = DateTimeOffset.UtcNow.AddDays(365).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var expiryStr = Ask("Bitis tarihi (yyyy-MM-dd HH:mm)", defaultExpiry);
        license.ExpiresAtUtc = ParseDateTimeOrDefault(expiryStr, DateTimeOffset.UtcNow.AddDays(365));

        var modulesInput = Ask("Aktif moduller (virgul ile, bos=tumunu ac)", string.Empty);
        license.EnabledModules = ParseModules(modulesInput);

        var profileInput = Ask("Fingerprint profili (PhysicalServer/Container)", "Container");
        var profile = ParseFingerprintProfile(profileInput);
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

        return Task.FromResult(new GeneratedLicense(license, null));
    }

    private static async Task<GeneratedLicense> BuildLicenseFromContextAsync(GenerateOptions options)
    {
        var context = await LoadContextAsync(options);
        var profile = ParseFingerprintProfile(context.FingerprintProfile);
        var customerName = !string.IsNullOrWhiteSpace(context.CustomerName)
            ? context.CustomerName.Trim()
            : context.CustomerCode.Trim();

        if (string.IsNullOrWhiteSpace(context.ProductCode))
        {
            throw new InvalidOperationException("Context icinde productCode bos olamaz.");
        }

        if (string.IsNullOrWhiteSpace(context.CustomerCode))
        {
            throw new InvalidOperationException("Context icinde customerCode bos olamaz.");
        }

        if (string.IsNullOrWhiteSpace(context.EnvironmentName))
        {
            throw new InvalidOperationException("Context icinde environmentName bos olamaz.");
        }

        if (string.IsNullOrWhiteSpace(context.InstanceId))
        {
            throw new InvalidOperationException("Context icinde instanceId bos olamaz.");
        }

        if (context.RequiresDeploymentMarker && string.IsNullOrWhiteSpace(context.DeploymentMarker))
        {
            throw new InvalidOperationException("Deployment marker gerekli ama context icinde bos.");
        }

        var license = new LicenseDocument
        {
            LicenseId = Guid.NewGuid().ToString("D"),
            LicenseVersion = 1,
            ProductCode = context.ProductCode.Trim(),
            CustomerCode = context.CustomerCode.Trim(),
            CustomerName = customerName,
            EnvironmentName = context.EnvironmentName.Trim(),
            InstanceId = context.InstanceId.Trim(),
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = ParseDateTimeOrDefault(options.ExpiresAtUtcText, DateTimeOffset.UtcNow.AddDays(365)),
            EnabledModules = ParseModules(options.EnabledModulesText),
            FingerprintHash = ResolveFingerprintHash(context, profile)
        };

        Console.WriteLine("=== TOD Lisans Uretici (Context Modu) ===\n");
        Console.WriteLine($"  Context File:   {options.ContextFilePath ?? "(inline json)"}");
        Console.WriteLine($"  Product Code:   {license.ProductCode}");
        Console.WriteLine($"  Customer Code:  {license.CustomerCode}");
        Console.WriteLine($"  Environment:    {license.EnvironmentName}");
        Console.WriteLine($"  Instance ID:    {license.InstanceId}");
        Console.WriteLine($"  Fingerprint:    {license.FingerprintHash}");

        return new GeneratedLicense(
            license,
            !string.IsNullOrWhiteSpace(options.OutputPath)
                ? options.OutputPath
                : (!string.IsNullOrWhiteSpace(context.LicenseFilePath) ? context.LicenseFilePath : null));
    }

    private static async Task<LicenseGenerationContextInput> LoadContextAsync(GenerateOptions options)
    {
        string json;
        if (!string.IsNullOrWhiteSpace(options.ContextJson))
        {
            json = options.ContextJson!;
        }
        else if (!string.IsNullOrWhiteSpace(options.ContextFilePath))
        {
            if (!File.Exists(options.ContextFilePath))
            {
                throw new FileNotFoundException($"Context dosyasi bulunamadi: {options.ContextFilePath}", options.ContextFilePath);
            }

            json = await File.ReadAllTextAsync(options.ContextFilePath);
        }
        else
        {
            throw new InvalidOperationException("Context modu icin --context-file veya --context-json gerekir.");
        }

        var context = JsonSerializer.Deserialize<LicenseGenerationContextInput>(json, LenientJson);
        if (context is null)
        {
            throw new InvalidOperationException("Context JSON okunanamadi.");
        }

        return context;
    }

    private static string ResolveFingerprintHash(LicenseGenerationContextInput context, FingerprintProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(context.RuntimeFingerprintHash))
        {
            return context.RuntimeFingerprintHash.Trim();
        }

        return LicenseGeneratorCore.ComputeFingerprintHash(
            profile,
            context.EnvironmentName,
            context.InstanceId,
            context.CustomerCode,
            context.DeploymentMarker);
    }

    private static GenerateOptions ParseGenerateOptions(string[] args)
    {
        var options = new GenerateOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--context-file":
                    options.ContextFilePath = RequireValue(args, ref i, arg);
                    break;
                case "--context-json":
                    options.ContextJson = RequireValue(args, ref i, arg);
                    break;
                case "--output":
                    options.OutputPath = RequireValue(args, ref i, arg);
                    break;
                case "--customer-name":
                    options.CustomerName = RequireValue(args, ref i, arg);
                    break;
                case "--expires-at-utc":
                    options.ExpiresAtUtcText = RequireValue(args, ref i, arg);
                    break;
                case "--enabled-modules":
                    options.EnabledModulesText = RequireValue(args, ref i, arg);
                    break;
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;
                default:
                    throw new ArgumentException($"Bilinmeyen arguman: {arg}");
            }
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            throw new ArgumentException($"{optionName} icin deger gerekli.");
        }

        index++;
        return args[index];
    }

    private static void ShowGenerateUsage()
    {
        Console.WriteLine("""
            generate modu
            -------------
            Interaktif:
              dotnet run -- generate

            JSON context file:
              dotnet run -- generate --context-file /work/license-context.json --output /work/license.json

            Inline JSON:
              dotnet run -- generate --context-json '{"productCode":"STYS",...}' --output /work/license.json

            Opsiyonel parametreler:
              --customer-name <ad>
              --expires-at-utc <yyyy-MM-dd HH:mm>
              --enabled-modules <modul1,modul2>
            """);
    }

    private static FingerprintProfile ParseFingerprintProfile(string? profileText)
    {
        return Enum.TryParse<FingerprintProfile>(profileText, true, out var parsedProfile)
            ? parsedProfile
            : FingerprintProfile.Container;
    }

    private static List<string> ParseModules(string? modulesText)
    {
        if (string.IsNullOrWhiteSpace(modulesText))
        {
            return [];
        }

        return modulesText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static DateTimeOffset ParseDateTimeOrDefault(string? value, DateTimeOffset fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return DateTimeOffset.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static string ResolveOutputPath(string? explicitOutputPath, string? suggestedOutputPath, LicenseDocument license)
    {
        var outputPath = !string.IsNullOrWhiteSpace(explicitOutputPath)
            ? explicitOutputPath.Trim()
            : !string.IsNullOrWhiteSpace(suggestedOutputPath)
                ? suggestedOutputPath.Trim()
                : $"license-{license.ProductCode}-{license.CustomerCode}.json".ToLowerInvariant();

        return outputPath;
    }

    private static void EnsureOutputDirectoryExists(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static void PrintLicenseSummary(LicenseDocument license, string outputPath)
    {
        Console.WriteLine($"\n--- Lisans uretildi: {outputPath} ---");
        Console.WriteLine($"  License ID:  {license.LicenseId}");
        Console.WriteLine($"  Gecerlilik:  {license.IssuedAtUtc:yyyy-MM-dd HH:mm} -> {license.ExpiresAtUtc:yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine($"  Moduller:    {(license.EnabledModules.Count == 0 ? "Tumunu ac" : string.Join(", ", license.EnabledModules))}");
    }

    private static int ShowFingerprint()
    {
        Console.WriteLine("=== Fingerprint Bilgileri ===\n");
        Console.WriteLine($"  Machine Name:     {Environment.MachineName}");
        Console.WriteLine($"  OS Description:   {RuntimeInformation.OSDescription}");

        var profileInput = Ask("Fingerprint profili (PhysicalServer/Container)", "Container");
        var profile = ParseFingerprintProfile(profileInput);
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

    private sealed record GenerateOptions
    {
        public string? ContextFilePath { get; set; }

        public string? ContextJson { get; set; }

        public string? OutputPath { get; set; }

        public string? CustomerName { get; set; }

        public string? ExpiresAtUtcText { get; set; }

        public string? EnabledModulesText { get; set; }

        public bool ShowHelp { get; set; }

        public bool HasContext => !string.IsNullOrWhiteSpace(ContextFilePath) || !string.IsNullOrWhiteSpace(ContextJson);
    }

    private sealed record GeneratedLicense(LicenseDocument License, string? SuggestedOutputPath);
}
