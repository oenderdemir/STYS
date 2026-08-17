using Microsoft.Extensions.Logging;

namespace STYS.Agent.Upgrade;

public enum UpdaterPresence
{
    /// <summary>The updater service is installed and can carry out an apply.</summary>
    Present = 0,

    /// <summary>The query succeeded and the service is definitively absent.</summary>
    Missing = 1,

    /// <summary>The query itself failed, so presence could not be established either way.</summary>
    Unknown = 2
}

public interface IUpdaterServicePresenceProbe
{
    UpdaterPresence Check(string serviceName);
}

/// <summary>
/// Queries the Windows service database for the updater service. Distinguishing "definitively
/// absent" from "could not tell" matters: the apply path refuses in both cases but reports a
/// different error code, so an operator can tell a missing install from a permissions problem.
/// </summary>
public sealed class WindowsUpdaterServicePresenceProbe : IUpdaterServicePresenceProbe
{
    private readonly ILogger<WindowsUpdaterServicePresenceProbe> _logger;

    public WindowsUpdaterServicePresenceProbe(ILogger<WindowsUpdaterServicePresenceProbe> logger)
    {
        _logger = logger;
    }

    public UpdaterPresence Check(string serviceName)
    {
        if (!OperatingSystem.IsWindows())
        {
            // Windows-only phase: nothing to assert about other platforms, so do not block them.
            return UpdaterPresence.Present;
        }

        try
        {
            return IsServiceInstalled(serviceName) ? UpdaterPresence.Present : UpdaterPresence.Missing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Updater servis durumu sorgulanamadı: {ServiceName}", serviceName);
            return UpdaterPresence.Unknown;
        }
    }

    // Separate method so the platform guard in Check() is visible to the platform-compatibility
    // analyzer, which cannot see through the LINQ closure.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsServiceInstalled(string serviceName) =>
        System.ServiceProcess.ServiceController
            .GetServices()
            .Any(x => string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.DisplayName, serviceName, StringComparison.OrdinalIgnoreCase));
}
