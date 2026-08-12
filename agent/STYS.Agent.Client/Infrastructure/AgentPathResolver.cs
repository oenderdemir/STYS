namespace STYS.Agent.Client.Infrastructure;

public interface IAgentPathResolver
{
    string DataDirectory { get; }
    string LogDirectory { get; }
    string BootstrapConfigurationPath { get; }
    string CredentialStorePath { get; }
    string LocalDevicesStorePath { get; }
    string LocalDeviceTerminalsStorePath { get; }
    string PavoPairingStorePath { get; }
    string AgentCommandExecutionStorePath { get; }
    string InstanceIdPath { get; }
}

public sealed class AgentPathResolver : IAgentPathResolver
{
    public string DataDirectory => AgentPaths.GetDataDirectory();
    public string LogDirectory => AgentPaths.GetLogDirectory();
    public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
    public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
    public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
    public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
    public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
    public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
    public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
}

internal static class AgentPaths
{
    private const string DataDirectoryEnvironmentVariable = "STYS_AGENT_DATA_DIR";
    private const string LogDirectoryEnvironmentVariable = "STYS_AGENT_LOG_DIR";

    public static string GetDataDirectory()
    {
        return ResolveDirectory(DataDirectoryEnvironmentVariable, GetDefaultDataDirectory);
    }

    public static string GetLogDirectory()
    {
        return ResolveDirectory(LogDirectoryEnvironmentVariable, GetDefaultLogDirectory);
    }

    private static string ResolveDirectory(string environmentVariable, Func<string> defaultFactory)
    {
        var configuredValue = Environment.GetEnvironmentVariable(environmentVariable);
        var directory = string.IsNullOrWhiteSpace(configuredValue) ? defaultFactory() : configuredValue;
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        SecureDirectory(directory);
        return directory;
    }

    private static string GetDefaultDataDirectory()
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
