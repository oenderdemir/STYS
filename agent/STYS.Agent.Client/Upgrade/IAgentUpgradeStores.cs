using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.Client.Upgrade;

public interface IAgentUpgradeRequestStore
{
    Task<AgentApplyUpgradeRequest?> GetAsync(CancellationToken cancellationToken);
    Task WriteAsync(AgentApplyUpgradeRequest request, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public interface IAgentUpgradeOutcomeStore
{
    Task<AgentUpgradeOutcome?> GetAsync(CancellationToken cancellationToken);
    Task WriteAsync(AgentUpgradeOutcome outcome, CancellationToken cancellationToken);
    Task MarkReportedAsync(Guid commandId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class FileAgentUpgradeRequestStore : IAgentUpgradeRequestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate;
    private readonly ILogger<FileAgentUpgradeRequestStore> _logger;

    public FileAgentUpgradeRequestStore(IAgentPathResolver paths, ILogger<FileAgentUpgradeRequestStore> logger)
    {
        _storePath = paths.UpgradeRequestPath;
        _gate = Gates.GetOrAdd(_storePath, _ => new SemaphoreSlim(1, 1));
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
    }

    public Task<AgentApplyUpgradeRequest?> GetAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(WithGate(ReadCore));
    }

    public Task WriteAsync(AgentApplyUpgradeRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            WriteCore(request);
            return 0;
        });
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
            return 0;
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

    private AgentApplyUpgradeRequest? ReadCore()
    {
        if (!File.Exists(_storePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(_storePath);
            return JsonSerializer.Deserialize<AgentApplyUpgradeRequest>(stream, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upgrade request store okunamadı.");
            throw new InvalidOperationException("Upgrade request store okunamadı.", ex);
        }
    }

    private void WriteCore(AgentApplyUpgradeRequest request)
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(_storePath)!, $"{Path.GetRandomFileName()}.tmp");
        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, request, JsonOptions);
            stream.Flush(true);
        }

        File.Move(tempPath, _storePath, true);
    }
}

public sealed class FileAgentUpgradeOutcomeStore : IAgentUpgradeOutcomeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate;
    private readonly ILogger<FileAgentUpgradeOutcomeStore> _logger;

    public FileAgentUpgradeOutcomeStore(IAgentPathResolver paths, ILogger<FileAgentUpgradeOutcomeStore> logger)
    {
        _storePath = paths.UpgradeOutcomePath;
        _gate = Gates.GetOrAdd(_storePath, _ => new SemaphoreSlim(1, 1));
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
    }

    public Task<AgentUpgradeOutcome?> GetAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(WithGate(ReadCore));
    }

    public Task WriteAsync(AgentUpgradeOutcome outcome, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            WriteCore(outcome);
            return 0;
        });
        return Task.CompletedTask;
    }

    public Task MarkReportedAsync(Guid commandId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            var current = ReadCore();
            if (current is null || current.CommandId != commandId)
            {
                return 0;
            }

            current.ReportedAt ??= DateTimeOffset.UtcNow;
            WriteCore(current);
            return 0;
        });
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        WithGate(() =>
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
            return 0;
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

    private AgentUpgradeOutcome? ReadCore()
    {
        if (!File.Exists(_storePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(_storePath);
            return JsonSerializer.Deserialize<AgentUpgradeOutcome>(stream, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upgrade outcome store okunamadı.");
            throw new InvalidOperationException("Upgrade outcome store okunamadı.", ex);
        }
    }

    private void WriteCore(AgentUpgradeOutcome outcome)
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(_storePath)!, $"{Path.GetRandomFileName()}.tmp");
        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, outcome, JsonOptions);
            stream.Flush(true);
        }

        File.Move(tempPath, _storePath, true);
    }
}
