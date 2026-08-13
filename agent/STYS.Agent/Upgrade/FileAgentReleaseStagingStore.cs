using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.Upgrade;

public sealed class FileAgentReleaseStagingStore : IAgentReleaseStagingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private readonly IAgentPathResolver _paths;
    private readonly string _storePath;
    private readonly SemaphoreSlim _gate;
    private readonly ILogger<FileAgentReleaseStagingStore> _logger;

    public FileAgentReleaseStagingStore(IAgentPathResolver paths, ILogger<FileAgentReleaseStagingStore> logger)
    {
        _paths = paths;
        _storePath = Path.Combine(paths.ReleaseStagingRootDirectory, "release-staging.json");
        _gate = Gates.GetOrAdd(_storePath, _ => new SemaphoreSlim(1, 1));
        _logger = logger;
        Directory.CreateDirectory(paths.ReleaseStagingRootDirectory);
    }

    public Task<AgentReleaseStagingState?> GetAsync(int releaseId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var key = BuildKey(releaseId);
        return Task.FromResult(WithGate(() =>
        {
            var items = ReadAllCore();
            return items.TryGetValue(key, out var state) ? Clone(state) : null;
        }));
    }

    public Task UpsertAsync(AgentReleaseStagingState state, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            var items = ReadAllCore();
            items[BuildKey(state.ReleaseId)] = Clone(state) ?? state;
            WriteAllCore(items);
        });
        return Task.CompletedTask;
    }

    private T WithGate<T>(Func<T> action)
    {
        _gate.Wait();
        try
        {
            return action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void WithGate(Action action)
    {
        _gate.Wait();
        try
        {
            action();
        }
        finally
        {
            _gate.Release();
        }
    }

    private Dictionary<string, AgentReleaseStagingState> ReadAllCore()
    {
        if (!File.Exists(_storePath))
        {
            return new Dictionary<string, AgentReleaseStagingState>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var stream = File.OpenRead(_storePath);
            var items = JsonSerializer.Deserialize<Dictionary<string, AgentReleaseStagingState>>(stream, JsonOptions);
            return items is null
                ? new Dictionary<string, AgentReleaseStagingState>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, AgentReleaseStagingState>(items, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent release staging store okunamadı.");
            throw new InvalidOperationException("Agent release staging store okunamadı.", ex);
        }
    }

    private void WriteAllCore(Dictionary<string, AgentReleaseStagingState> items)
    {
        Directory.CreateDirectory(_paths.ReleaseStagingRootDirectory);
        var tempPath = Path.Combine(_paths.ReleaseStagingRootDirectory, $"{Path.GetRandomFileName()}.tmp");
        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, items, JsonOptions);
            stream.Flush(true);
        }

        ReplaceAtomically(tempPath, _storePath);
    }

    private static AgentReleaseStagingState? Clone(AgentReleaseStagingState? state) => state is null
        ? null
        : new AgentReleaseStagingState
        {
            ReleaseId = state.ReleaseId,
            Version = state.Version,
            ContractVersion = state.ContractVersion,
            RuntimeIdentifier = state.RuntimeIdentifier,
            StageStatus = state.StageStatus,
            Message = state.Message,
            Sha256 = state.Sha256,
            Signature = state.Signature,
            PackageSize = state.PackageSize,
            PublishedAt = state.PublishedAt,
            PackagePath = state.PackagePath,
            DownloadingAt = state.DownloadingAt,
            VerifyingAt = state.VerifyingAt,
            StagedAt = state.StagedAt,
            FailedAt = state.FailedAt,
            UpdatedAt = state.UpdatedAt
        };

    private static string BuildKey(int releaseId) =>
        releaseId.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void ReplaceAtomically(string tempPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(tempPath, targetPath);
    }
}
