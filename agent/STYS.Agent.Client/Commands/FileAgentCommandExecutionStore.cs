using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.Client.Commands;

public sealed class FileAgentCommandExecutionStore : IAgentCommandExecutionStore
{
    private static readonly string[] PersistentKeyPrefixes =
    [
        "PavoStartPayment:",
        "PavoGetPaymentResult:"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _storePath;
    private readonly SemaphoreSlim _gate;
    private readonly MemoryAgentCommandExecutionStore _memoryFallback = new();
    private readonly ILogger<FileAgentCommandExecutionStore> _logger;

    public FileAgentCommandExecutionStore(IAgentPathResolver paths, ILogger<FileAgentCommandExecutionStore> logger)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        SecureDirectory(paths.DataDirectory);
        _storePath = paths.AgentCommandExecutionStorePath;
        _gate = Gates.GetOrAdd(_storePath, _ => new SemaphoreSlim(1, 1));
        _logger = logger;
    }

    public bool HasExecuted(string idempotencyKey)
    {
        var normalized = NormalizeKey(idempotencyKey);
        if (normalized is null)
        {
            return false;
        }

        if (!IsPersistentKey(normalized))
        {
            return _memoryFallback.HasExecuted(normalized);
        }

        return WithGate(() =>
        {
            var items = ReadAllCore();
            return items.TryGetValue(normalized, out var state) && (state.Started || state.Result is not null);
        });
    }

    public void MarkExecuted(string idempotencyKey)
    {
        var normalized = NormalizeKey(idempotencyKey) ?? throw new InvalidOperationException("IdempotencyKey zorunludur.");

        if (!IsPersistentKey(normalized))
        {
            _memoryFallback.MarkExecuted(normalized);
            return;
        }

        WithGate(() =>
        {
            var items = ReadAllCore();
            if (!items.TryGetValue(normalized, out var state))
            {
                state = new AgentCommandExecutionState();
                items[normalized] = state;
            }

            state.Started = true;
            state.StartedAt ??= DateTimeOffset.UtcNow;
            WriteAllCore(items);
        });
    }

    public AgentCommandResult? GetResult(string idempotencyKey)
    {
        var normalized = NormalizeKey(idempotencyKey);
        if (normalized is null)
        {
            return null;
        }

        if (!IsPersistentKey(normalized))
        {
            return _memoryFallback.GetResult(normalized);
        }

        return WithGate(() =>
        {
            var items = ReadAllCore();
            return items.TryGetValue(normalized, out var state) ? Clone(state.Result) : null;
        });
    }

    public void StoreResult(string idempotencyKey, AgentCommandResult result)
    {
        var normalized = NormalizeKey(idempotencyKey) ?? throw new InvalidOperationException("IdempotencyKey zorunludur.");
        var cloned = Clone(result) ?? throw new InvalidOperationException("Agent command result zorunludur.");

        if (!IsPersistentKey(normalized))
        {
            _memoryFallback.StoreResult(normalized, cloned);
            return;
        }

        WithGate(() =>
        {
            var items = ReadAllCore();
            if (!items.TryGetValue(normalized, out var state))
            {
                state = new AgentCommandExecutionState();
                items[normalized] = state;
            }

            state.Started = true;
            state.StartedAt ??= DateTimeOffset.UtcNow;
            state.Result = cloned;
            state.ResultStoredAt = DateTimeOffset.UtcNow;
            WriteAllCore(items);
        });
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

    private Dictionary<string, AgentCommandExecutionState> ReadAllCore()
    {
        if (!File.Exists(_storePath))
        {
            return new Dictionary<string, AgentCommandExecutionState>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var stream = File.OpenRead(_storePath);
            var items = JsonSerializer.Deserialize<Dictionary<string, AgentCommandExecutionState>>(stream, JsonOptions);
            return items is null
                ? new Dictionary<string, AgentCommandExecutionState>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, AgentCommandExecutionState>(items, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persisted agent command execution store is unreadable or corrupted.");
            throw new InvalidOperationException("Persisted agent command execution store is unreadable or corrupted.", ex);
        }
    }

    private void WriteAllCore(Dictionary<string, AgentCommandExecutionState> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath) ?? AppContext.BaseDirectory);
        var tempPath = Path.Combine(Path.GetDirectoryName(_storePath) ?? AppContext.BaseDirectory, $"{Path.GetRandomFileName()}.tmp");

        using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, items, JsonOptions);
            stream.Flush(true);
        }

        if (File.Exists(_storePath))
        {
            File.Move(tempPath, _storePath, true);
        }
        else
        {
            File.Move(tempPath, _storePath);
        }

        SecureFile(_storePath);
    }

    private static string? NormalizeKey(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool IsPersistentKey(string key) =>
        PersistentKeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static AgentCommandResult? Clone(AgentCommandResult? result) => result is null
        ? null
        : new AgentCommandResult
        {
            Success = result.Success,
            ResultPayload = result.ResultPayload,
            ErrorCode = result.ErrorCode,
            ErrorMessage = result.ErrorMessage
        };

    private static void SecureFile(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
        }
    }

    private static void SecureDirectory(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch
        {
        }
    }
}

internal sealed class AgentCommandExecutionState
{
    public bool Started { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public AgentCommandResult? Result { get; set; }
    public DateTimeOffset? ResultStoredAt { get; set; }
}
