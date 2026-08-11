using System.Text.Json;
using STYS.Agent.Client.Infrastructure;
using Microsoft.Extensions.Logging;

namespace STYS.Agent.Configuration;

public sealed class FileAgentBootstrapConfigurationStore : IAgentBootstrapConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentPathResolver _paths;
    private readonly ILogger<FileAgentBootstrapConfigurationStore> _logger;

    public FileAgentBootstrapConfigurationStore(IAgentPathResolver paths, ILogger<FileAgentBootstrapConfigurationStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<AgentBootstrapConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        var configuration = await TryGetAsync(cancellationToken);
        return configuration ?? ApplyDefaults(new AgentBootstrapConfiguration());
    }

    public async Task<AgentBootstrapConfiguration?> TryGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_paths.BootstrapConfigurationPath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_paths.BootstrapConfigurationPath);
            var configuration = await JsonSerializer.DeserializeAsync<AgentBootstrapConfiguration>(stream, JsonOptions, cancellationToken)
                ?? new AgentBootstrapConfiguration();
            return ApplyDefaults(configuration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bootstrap configuration could not be read; defaults will be used.");
            return null;
        }
    }

    public async Task SaveAsync(AgentBootstrapConfiguration configuration, CancellationToken cancellationToken)
    {
        var normalized = ApplyDefaults(configuration);
        Directory.CreateDirectory(_paths.DataDirectory);
        await using var stream = File.Create(_paths.BootstrapConfigurationPath);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        SecureFile(_paths.BootstrapConfigurationPath);
    }

    private static AgentBootstrapConfiguration ApplyDefaults(AgentBootstrapConfiguration configuration)
    {
        configuration.StysBaseUrl = NormalizeBaseUrl(configuration.StysBaseUrl);
        configuration.LocalUiPort = configuration.LocalUiPort <= 0 ? 5180 : configuration.LocalUiPort;
        configuration.HttpTimeoutSeconds = configuration.HttpTimeoutSeconds <= 0 ? 30 : configuration.HttpTimeoutSeconds;
        configuration.AgentDisplayName = string.IsNullOrWhiteSpace(configuration.AgentDisplayName)
            ? Environment.MachineName
            : configuration.AgentDisplayName.Trim();
        return configuration;
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "https://localhost:7160" : baseUrl.Trim();
        return value.TrimEnd('/');
    }

    private static void SecureFile(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
    }
}
