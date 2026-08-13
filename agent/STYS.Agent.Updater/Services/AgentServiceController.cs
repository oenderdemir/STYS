using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using STYS.Agent.Updater.Options;

namespace STYS.Agent.Updater.Services;

public interface IAgentServiceController
{
    Task StopAsync(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task<bool> WaitForStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken);
    Task<bool> WaitForRunningAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class AgentServiceController : IAgentServiceController
{
    private readonly AgentUpgradeRuntimeOptions _options;
    private readonly ILogger<AgentServiceController> _logger;

    public AgentServiceController(AgentUpgradeRuntimeOptions options, ILogger<AgentServiceController> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StopAsync(CancellationToken cancellationToken) => ExecuteAsync("stop", cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken) => ExecuteAsync("start", cancellationToken);

    public async Task<bool> WaitForStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (!IsRunning())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return !IsRunning();
    }

    public async Task<bool> WaitForRunningAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (IsRunning())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        return IsRunning();
    }

    private bool IsRunning()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var controller = new ServiceController(_options.ServiceName);
                return controller.Status == ServiceControllerStatus.Running || controller.Status == ServiceControllerStatus.StartPending;
            }
            catch
            {
                return false;
            }
        }

        var result = RunProcess("systemctl", $"is-active {_options.ServiceName}", captureOutput: true);
        return result.ExitCode == 0 && string.Equals(result.StandardOutput.Trim(), "active", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteAsync(string action, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            using var controller = new ServiceController(_options.ServiceName);
            if (action == "stop")
            {
                if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
                {
                    return;
                }

                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                return;
            }

            if (controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            {
                return;
            }

            controller.Start();
            controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(60));
            return;
        }

        var result = RunProcess("systemctl", $"{action} {_options.ServiceName}", captureOutput: true, cancellationToken);
        if (result.ExitCode != 0)
        {
            _logger.LogWarning("systemctl {Action} failed: {Error}", action, result.StandardError.Trim());
        }
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunProcess(
        string fileName,
        string arguments,
        bool captureOutput,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Process başlatılamadı: {fileName}");
        if (!captureOutput)
        {
            process.WaitForExit();
            return (process.ExitCode, string.Empty, string.Empty);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }
}
