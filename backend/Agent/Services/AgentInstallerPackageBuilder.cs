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

    public static byte[] Build(AgentInstallationSession session, string baseUrl)
    {
        var publishRid = NormalizeRid(session.TargetRid);
        var packageSource = ResolvePackageSource(publishRid);
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
            AddTextFile(archive, "install-stys-agent.ps1", ReadRequiredText(packageSource, Path.Combine("scripts", "install-stys-agent.ps1"), Path.Combine("scripts", "agent", "install-stys-agent.ps1")));
            AddTextFile(archive, "scripts/install-agent.ps1", ReadRequiredText(packageSource, Path.Combine("scripts", "install-agent.ps1"), Path.Combine("scripts", "agent", "install-agent.ps1")));
            AddTextFile(archive, "scripts/install-agent-updater.ps1", ReadRequiredText(packageSource, Path.Combine("scripts", "install-agent-updater.ps1"), Path.Combine("scripts", "agent", "install-agent-updater.ps1")));

            AddDirectory(archive, packageSource.AgentPublishRoot, "agent");
            AddDirectory(archive, packageSource.UpdaterPublishRoot, "updater");
            AddJsonFile(archive, "config/bootstrap.json", bootstrap);
            AddTextFile(archive, "README.txt", BuildReadme(session, bootstrap));
            AddTrustAnchor(archive, packageSource);
        }

        return memoryStream.ToArray();
    }

    private static void AddTrustAnchor(ZipArchive archive, InstallerPackageSource source)
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

        var rootTrustPath = Path.Combine(source.RootDirectory, "trust", "release-public-key.pem");
        if (File.Exists(rootTrustPath))
        {
            AddTextFile(archive, "trust/release-public-key.pem", File.ReadAllText(rootTrustPath));
            return;
        }

        if (!source.IsConfiguredRoot)
        {
            var repoTrustPath = Path.Combine(source.RepositoryRoot, "trust", "release-public-key.pem");
            if (File.Exists(repoTrustPath))
            {
                AddTextFile(archive, "trust/release-public-key.pem", File.ReadAllText(repoTrustPath));
                return;
            }
        }

        // Fail closed, but tell the operator which locations were tried and in what order. Without
        // this the only signal is "not found", which does not say what to provision or where.
        var searched = new List<string>
        {
            "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM (inline PEM ortam değişkeni)",
            "STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH" + (string.IsNullOrWhiteSpace(configuredPath)
                ? " (tanımlı değil)"
                : $" -> {Path.GetFullPath(configuredPath.Trim())} (dosya yok)"),
            rootTrustPath
        };

        if (!source.IsConfiguredRoot)
        {
            searched.Add(Path.Combine(source.RepositoryRoot, "trust", "release-public-key.pem"));
        }

        throw new InvalidOperationException(
            "Release public key bulunamadı. Installer paketi trust anchor olmadan üretilemez. " +
            $"Aranan konumlar: {string.Join(" | ", searched)}. " +
            "Provisioning için bkz. docs/agent-production-installation.md (\"Trust boundary and release key provisioning\").");
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

    private static InstallerPackageSource ResolvePackageSource(string runtimeIdentifier)
    {
        var normalizedRid = NormalizeRid(runtimeIdentifier);
        var configuredRoot = Environment.GetEnvironmentVariable("STYS_AGENT_INSTALLER_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var root = Path.GetFullPath(configuredRoot.Trim());
            return new InstallerPackageSource(
                RootDirectory: root,
                RepositoryRoot: string.Empty,
                IsConfiguredRoot: true,
                AgentPublishRoot: RequirePublishDirectory(root, normalizedRid, "agent"),
                UpdaterPublishRoot: RequirePublishDirectory(root, normalizedRid, "updater"));
        }

        var repoRoot = ResolveRepositoryRoot();
        var deployRoot = Path.Combine(repoRoot, "artifacts", "deploy");
        return new InstallerPackageSource(
            RootDirectory: deployRoot,
            RepositoryRoot: repoRoot,
            IsConfiguredRoot: false,
            AgentPublishRoot: RequirePublishDirectory(deployRoot, normalizedRid, "agent"),
            UpdaterPublishRoot: RequirePublishDirectory(deployRoot, normalizedRid, "updater"));
    }

    private static string RequirePublishDirectory(string root, string runtimeIdentifier, string kind)
    {
        var candidate = Path.Combine(root, runtimeIdentifier, kind);
        if (Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories).Any())
        {
            return candidate;
        }

        throw new DirectoryNotFoundException($"installer root missing {runtimeIdentifier} {kind} artifact: {candidate}");
    }

    private static string ReadRequiredText(InstallerPackageSource source, string configuredRelativePath, string fallbackRelativePath)
    {
        var configuredPath = Path.Combine(source.RootDirectory, configuredRelativePath);
        if (source.IsConfiguredRoot)
        {
            if (!File.Exists(configuredPath))
            {
                throw new FileNotFoundException($"configured installer root missing {configuredRelativePath}: {configuredPath}", configuredPath);
            }

            return File.ReadAllText(configuredPath);
        }

        if (File.Exists(configuredPath))
        {
            return File.ReadAllText(configuredPath);
        }

        var fallbackPath = Path.Combine(source.RepositoryRoot, fallbackRelativePath);
        if (!File.Exists(fallbackPath))
        {
            throw new FileNotFoundException($"Installer paketi için gerekli dosya bulunamadı: {fallbackPath}", fallbackPath);
        }

        return File.ReadAllText(fallbackPath);
    }

    private static void AddJsonFile(ZipArchive archive, string entryPath, object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        AddTextFile(archive, entryPath, json);
    }

    private static void AddTextFile(ZipArchive archive, string entryPath, string content)
    {
        var normalizedPath = NormalizeArchivePath(entryPath);
        var entry = archive.CreateEntry(normalizedPath, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, ResolveTextEncoding(normalizedPath));
        writer.Write(content);
    }

    /// <summary>
    /// Windows PowerShell 5.1 assumes the active ANSI codepage for scripts with no byte order mark,
    /// so UTF-8 Turkish literals render as mojibake ("başlıyor" becomes "baÅŸlÄ±yor"). Scripts are
    /// therefore emitted with a BOM. File.ReadAllText strips the BOM off the repository copies, so
    /// this is what puts it back on the packaged ones. JSON and PEM entries must stay BOM-free:
    /// System.Text.Json rejects a leading BOM and PEM import expects the header first.
    /// </summary>
    private static Encoding ResolveTextEncoding(string normalizedEntryPath) =>
        normalizedEntryPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
            "Run install-stys-agent.ps1 on Windows.\r\n" +
            "Enrollment code is requested interactively and is not stored in this package.\r\n";
    }

    private static string ResolvePackageVersion()
    {
        var assembly = typeof(AgentInstallerPackageBuilder).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Trim()
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "README.md"))
                && Directory.Exists(Path.Combine(current.FullName, "scripts", "agent")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository kökü bulunamadı. Installer paketi üretilemedi.");
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

    private sealed record InstallerPackageSource(
        string RootDirectory,
        string RepositoryRoot,
        bool IsConfiguredRoot,
        string AgentPublishRoot,
        string UpdaterPublishRoot);

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
