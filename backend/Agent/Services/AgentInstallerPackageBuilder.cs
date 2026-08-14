using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using STYS.Agent.Entities;

namespace STYS.Agent.Services;

internal static class AgentInstallerPackageBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static byte[] Build(AgentInstallationSession session, string baseUrl, string repoRoot)
    {
        var publishRid = NormalizeRid(session.TargetRid);
        var agentPublishRoot = ResolvePackageSource(repoRoot, "agent", publishRid);
        var updaterPublishRoot = ResolvePackageSource(repoRoot, "updater", publishRid);
        var installVersion = ResolvePackageVersion();
        var bootstrap = new InstallerBootstrapConfiguration
        {
            StysBaseUrl = NormalizeBaseUrl(baseUrl),
            LocalUiPort = 5180,
            AgentDisplayName = session.AgentDisplayName,
            InstallationSessionId = session.Id,
            TargetRid = publishRid,
            PackageVersion = installVersion,
            HttpTimeoutSeconds = 30
        };

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            AddTextFile(archive, "install-stys-agent.ps1", ReadRequiredFile(repoRoot, Path.Combine("scripts", "agent", "install-stys-agent.ps1")));
            AddTextFile(archive, "install-stys-agent.sh", ReadRequiredFile(repoRoot, Path.Combine("scripts", "agent", "install-stys-agent.sh")));

            AddDirectory(archive, Path.Combine(repoRoot, "scripts", "agent"), "scripts/agent");
            AddDirectory(archive, agentPublishRoot, "agent");
            AddDirectory(archive, updaterPublishRoot, "updater");
            AddJsonFile(archive, "config/bootstrap.json", bootstrap);
            AddTextFile(archive, "README.txt", BuildReadme(session, bootstrap));
            AddTrustAnchor(archive, repoRoot);
        }

        return memoryStream.ToArray();
    }

    private static void AddTrustAnchor(ZipArchive archive, string repoRoot)
    {
        var inlinePem = Environment.GetEnvironmentVariable("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM");
        if (!string.IsNullOrWhiteSpace(inlinePem))
        {
            AddTextFile(archive, "trust/release-public-key.pem", inlinePem.Trim());
            return;
        }

        var configuredPath = Environment.GetEnvironmentVariable("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var resolvedPath = Path.GetFullPath(configuredPath.Trim());
            if (File.Exists(resolvedPath))
            {
                AddTextFile(archive, "trust/release-public-key.pem", File.ReadAllText(resolvedPath));
                return;
            }
        }

        var repoTrustPath = Path.Combine(repoRoot, "trust", "release-public-key.pem");
        if (File.Exists(repoTrustPath))
        {
            AddTextFile(archive, "trust/release-public-key.pem", File.ReadAllText(repoTrustPath));
            return;
        }

        throw new InvalidOperationException("Release public key bulunamadı. Installer paketi trust anchor olmadan üretilemez.");
    }

    private static void AddDirectory(ZipArchive archive, string sourceDirectory, string archiveDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Installer paketi kaynağı bulunamadı: {sourceDirectory}");
        }

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
            if (relativePath.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || relativePath.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entryPath = CombineArchivePath(archiveDirectory, relativePath);
            AddBinaryFile(archive, entryPath, File.ReadAllBytes(filePath));
        }
    }

    private static string ResolvePackageSource(string repoRoot, string kind, string runtimeIdentifier)
    {
        var normalizedRid = NormalizeRid(runtimeIdentifier);
        var candidates = kind.Equals("agent", StringComparison.OrdinalIgnoreCase)
            ? new[]
            {
                Path.Combine(repoRoot, "artifacts", "agent", normalizedRid),
                Path.Combine(repoRoot, "agent", "STYS.Agent", "bin", "Release", "net10.0")
            }
            : new[]
            {
                Path.Combine(repoRoot, "artifacts", "agent-updater", normalizedRid),
                Path.Combine(repoRoot, "agent", "STYS.Agent.Updater", "bin", "Release", "net10.0")
            };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories).Any())
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Uygun {kind} publish çıktısı bulunamadı. Beklenen RID: {normalizedRid}");
    }

    private static void AddJsonFile(ZipArchive archive, string entryPath, object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        AddTextFile(archive, entryPath, json);
    }

    private static void AddTextFile(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(NormalizeArchivePath(entryPath), CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void AddBinaryFile(ZipArchive archive, string entryPath, byte[] content)
    {
        var entry = archive.CreateEntry(NormalizeArchivePath(entryPath), CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string BuildReadme(AgentInstallationSession session, InstallerBootstrapConfiguration bootstrap)
    {
        return
            $"STYS Agent unified installer package\r\n" +
            $"Session Id: {session.Id}\r\n" +
            $"Target RID: {bootstrap.TargetRid}\r\n" +
            $"Agent Display Name: {bootstrap.AgentDisplayName}\r\n" +
            $"Package Version: {bootstrap.PackageVersion}\r\n" +
            "\r\n" +
            "Run install-stys-agent.ps1 on Windows or install-stys-agent.sh on Linux.\r\n" +
            "Enrollment code is requested interactively and is not stored in this package.\r\n";
    }

    private static string ResolvePackageVersion()
    {
        var assembly = typeof(AgentInstallerPackageBuilder).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Trim()
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.Trim().TrimEnd('/');

    private static string NormalizeRid(string runtimeIdentifier)
    {
        var normalized = runtimeIdentifier.Trim();
        if (!normalized.Equals("win-x64", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("linux-x64", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Desteklenmeyen RID: {runtimeIdentifier}");
        }

        return normalized.ToLowerInvariant();
    }

    private static string NormalizeArchivePath(string entryPath) =>
        entryPath.Replace('\\', '/').TrimStart('/');

    private static string CombineArchivePath(string left, string right) =>
        NormalizeArchivePath(Path.Combine(left, right));

    private static string ReadRequiredFile(string repoRoot, string relativePath)
    {
        var filePath = Path.Combine(repoRoot, relativePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Installer paketi için gerekli dosya bulunamadı: {filePath}", filePath);
        }

        return File.ReadAllText(filePath);
    }

    private sealed class InstallerBootstrapConfiguration
    {
        public string StysBaseUrl { get; set; } = string.Empty;
        public int LocalUiPort { get; set; }
        public string AgentDisplayName { get; set; } = string.Empty;
        public int? InstallationSessionId { get; set; }
        public string? TargetRid { get; set; }
        public string? PackageVersion { get; set; }
        public int HttpTimeoutSeconds { get; set; }
    }
}
