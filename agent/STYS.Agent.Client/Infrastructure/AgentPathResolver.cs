namespace STYS.Agent.Client.Infrastructure;

public interface IAgentPathResolver
{
    string DataDirectory { get; }
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
    public static string GetDataDirectory()
    {
        var baseDirectory = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = AppContext.BaseDirectory;
        }

        var directory = Path.Combine(baseDirectory, "STYS", "Agent");
        Directory.CreateDirectory(directory);
        SecureDirectory(directory);
        return directory;
    }

    private static void SecureDirectory(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
        catch { }
    }
}
