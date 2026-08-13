namespace STYS.Agent.Client.Infrastructure;

public interface IAgentPathResolver
{
    string DataDirectory { get; }
    string SharedDataDirectory => DataDirectory;
    string UpdaterPrivateDataDirectory => AgentPaths.GetUpdaterPrivateDataDirectory(SharedDataDirectory);
    string LogDirectory { get; }
    string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
    string BootstrapConfigurationPath { get; }
    string CredentialStorePath { get; }
    string LocalDevicesStorePath { get; }
    string LocalDeviceTerminalsStorePath { get; }
    string PavoPairingStorePath { get; }
    string AgentCommandExecutionStorePath { get; }
    string UpgradeRequestPath => Path.Combine(DataDirectory, "updates", "apply-request.json");
    string UpgradeOutcomePath => Path.Combine(DataDirectory, "updates", "apply-outcome.json");
    string UpgradeBackupRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "backup");
    string UpgradeExtractRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "extract");
    string UpgradeTempRootDirectory => Path.Combine(UpdaterPrivateDataDirectory, "updates", "temp");
    string InstanceIdPath { get; }
    string GetReleaseStagingDirectory(string version, string runtimeIdentifier) =>
        Path.Combine(ReleaseStagingRootDirectory, AgentPaths.SanitizePathSegment(version), AgentPaths.SanitizePathSegment(runtimeIdentifier));
    string GetReleaseStagingStatePath(string version, string runtimeIdentifier) =>
        Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
    string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) =>
        Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
}

public sealed class AgentPathResolver : IAgentPathResolver
{
    public string DataDirectory => SharedDataDirectory;
    public string SharedDataDirectory => AgentPaths.GetSharedDataDirectory();
    public string UpdaterPrivateDataDirectory => AgentPaths.GetUpdaterPrivateDataDirectory(SharedDataDirectory);
    public string LogDirectory => AgentPaths.GetLogDirectory();
    public string ReleaseStagingRootDirectory => Path.Combine(SharedDataDirectory, "updates", "staging");
    public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
    public string CredentialStorePath => Path.Combine(SharedDataDirectory, "credential.dat");
    public string LocalDevicesStorePath => Path.Combine(SharedDataDirectory, "local-devices.json");
    public string LocalDeviceTerminalsStorePath => Path.Combine(SharedDataDirectory, "local-device-terminals.json");
    public string PavoPairingStorePath => Path.Combine(SharedDataDirectory, "pavo-pairing.dat");
    public string AgentCommandExecutionStorePath => Path.Combine(SharedDataDirectory, "agent-command-executions.json");
    public string InstanceIdPath => Path.Combine(SharedDataDirectory, "instance.id");
    public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) =>
        Path.Combine(ReleaseStagingRootDirectory, AgentPaths.SanitizePathSegment(version), AgentPaths.SanitizePathSegment(runtimeIdentifier));
    public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) =>
        Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
    public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) =>
        Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
}

internal static class AgentPaths
{
    private const string SharedDataDirectoryEnvironmentVariable = "STYS_AGENT_SHARED_DATA_DIR";
    private const string LegacyDataDirectoryEnvironmentVariable = "STYS_AGENT_DATA_DIR";
    private const string UpdaterPrivateDataDirectoryEnvironmentVariable = "STYS_AGENT_UPDATER_PRIVATE_DATA_DIR";
    private const string LogDirectoryEnvironmentVariable = "STYS_AGENT_LOG_DIR";

    public static string GetSharedDataDirectory()
    {
        return ResolveDirectory(SharedDataDirectoryEnvironmentVariable, GetDefaultSharedDataDirectory, LegacyDataDirectoryEnvironmentVariable);
    }

    public static string GetUpdaterPrivateDataDirectory(string sharedDataDirectory)
    {
        return ResolveDirectory(
            UpdaterPrivateDataDirectoryEnvironmentVariable,
            () => GetDefaultUpdaterPrivateDataDirectory(sharedDataDirectory));
    }

    public static string GetLogDirectory()
    {
        return ResolveDirectory(LogDirectoryEnvironmentVariable, GetDefaultLogDirectory);
    }

    public static string SanitizePathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = normalized.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private static string ResolveDirectory(string environmentVariable, Func<string> defaultFactory, params string[] fallbackEnvironmentVariables)
    {
        var configuredValue = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            foreach (var fallback in fallbackEnvironmentVariables)
            {
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    continue;
                }

                configuredValue = Environment.GetEnvironmentVariable(fallback);
                if (!string.IsNullOrWhiteSpace(configuredValue))
                {
                    break;
                }
            }
        }

        var directory = string.IsNullOrWhiteSpace(configuredValue) ? defaultFactory() : configuredValue;
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        SecureDirectory(directory);
        return directory;
    }

    private static string GetDefaultSharedDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "STYS", "Agent");
        }

        if (OperatingSystem.IsLinux())
        {
            return "/var/lib/stys-agent";
        }

        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "STYS", "Agent");
    }

    private static string GetDefaultUpdaterPrivateDataDirectory(string sharedDataDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "STYS", "AgentUpdater", "private");
        }

        if (OperatingSystem.IsLinux())
        {
            return "/var/lib/stys-agent-updater";
        }

        return Path.Combine(sharedDataDirectory, "updater-private");
    }

    private static string GetDefaultLogDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "STYS", "Agent", "logs");
        }

        if (OperatingSystem.IsLinux())
        {
            return "/var/log/stys-agent";
        }

        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        return Path.Combine(baseDirectory, "STYS", "Agent", "logs");
    }

    private static void SecureDirectory(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { }
    }
}
