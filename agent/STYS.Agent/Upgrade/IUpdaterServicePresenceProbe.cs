using Microsoft.Extensions.Logging;

namespace STYS.Agent.Upgrade;

public enum UpdaterPresence
{
    /// <summary>The updater service is installed and running, so it can carry out an apply.</summary>
    Present = 0,

    /// <summary>The query succeeded and the service is definitively absent.</summary>
    Missing = 1,

    /// <summary>The query itself failed, so presence could not be established either way.</summary>
    Unknown = 2,

    /// <summary>
    /// Installed but not running. A stopped service never reads the apply request, which would
    /// leave the command deferred until it expired, so this is reported separately from Missing to
    /// point the operator at starting the service rather than reinstalling it.
    /// </summary>
    NotRunning = 3
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
            return Resolve(serviceName);
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
    private static UpdaterPresence Resolve(string serviceName)
    {
        using var service = System.ServiceProcess.ServiceController
            .GetServices()
            .FirstOrDefault(x => string.Equals(x.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.DisplayName, serviceName, StringComparison.OrdinalIgnoreCase));

        if (service is null)
        {
            return UpdaterPresence.Missing;
        }

        // StartPending counts as running: the service is coming up and will read the request.
        return service.Status is System.ServiceProcess.ServiceControllerStatus.Running
            or System.ServiceProcess.ServiceControllerStatus.StartPending
            ? UpdaterPresence.Present
            : UpdaterPresence.NotRunning;
    }
}
