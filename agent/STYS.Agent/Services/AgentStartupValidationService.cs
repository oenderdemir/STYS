using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.Services;

public interface IAgentStartupValidationService
{
    Task<AgentStartupValidationResult> ValidateAsync(CancellationToken cancellationToken);
}

public sealed record AgentStartupValidationResult(
    bool IsHealthy,
    string Message,
    IReadOnlyCollection<string> CheckedPaths);

public sealed class AgentStartupValidationService : IAgentStartupValidationService
{
    private readonly IAgentPathResolver _paths;
    private readonly IAgentRuntimeStatus _runtimeStatus;
    private readonly ILogger<AgentStartupValidationService> _logger;

    public AgentStartupValidationService(
        IAgentPathResolver paths,
        IAgentRuntimeStatus runtimeStatus,
        ILogger<AgentStartupValidationService> logger)
    {
        _paths = paths;
        _runtimeStatus = runtimeStatus;
        _logger = logger;
    }

    public Task<AgentStartupValidationResult> ValidateAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var checkedPaths = new List<string>();
        try
        {
            ValidateWritableDirectory(_paths.DataDirectory, checkedPaths);
            ValidateWritableDirectory(_paths.LogDirectory, checkedPaths);
            ValidateWritableLocation(_paths.BootstrapConfigurationPath, checkedPaths);
            ValidateWritableLocation(_paths.CredentialStorePath, checkedPaths);
            ValidateWritableLocation(_paths.LocalDevicesStorePath, checkedPaths);
            ValidateWritableLocation(_paths.LocalDeviceTerminalsStorePath, checkedPaths);
            ValidateWritableLocation(_paths.PavoPairingStorePath, checkedPaths);
            ValidateWritableLocation(_paths.AgentCommandExecutionStorePath, checkedPaths);
            ValidateWritableLocation(_paths.InstanceIdPath, checkedPaths);

            _runtimeStatus.MarkStartupHealthy();
            return Task.FromResult(new AgentStartupValidationResult(true, "Critical stores writable.", checkedPaths));
        }
        catch (Exception ex)
        {
            var message = "Critical store validation failed.";
            _runtimeStatus.MarkStartupUnhealthy(message);
            _logger.LogError(ex, "Agent startup validation failed for critical storage paths.");
            return Task.FromResult(new AgentStartupValidationResult(false, message, checkedPaths));
        }
    }

    private static void ValidateWritableDirectory(string directory, ICollection<string> checkedPaths)
    {
        checkedPaths.Add(directory);

        Directory.CreateDirectory(directory);

        var probePath = Path.Combine(directory, $".stys-agent-{Guid.NewGuid():N}.probe");
        try
        {
            using var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            stream.WriteByte(0x2A);
            stream.Flush(true);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
            catch
            {
            }
        }
    }

    private static void ValidateWritableLocation(string path, ICollection<string> checkedPaths)
    {
        checkedPaths.Add(path);

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Unable to determine parent directory for '{path}'.");
        }

        ValidateWritableDirectory(directory, checkedPaths);
    }
}
