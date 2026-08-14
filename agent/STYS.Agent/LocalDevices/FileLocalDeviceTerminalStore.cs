using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.LocalDevices;

public sealed class FileLocalDeviceTerminalStore : ILocalDeviceTerminalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentPathResolver _paths;
    private readonly ILogger<FileLocalDeviceTerminalStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileLocalDeviceTerminalStore(IAgentPathResolver paths, ILogger<FileLocalDeviceTerminalStore> logger)
    {
        _paths = paths;
        _logger = logger;
        Directory.CreateDirectory(paths.DataDirectory);
    }

    public async Task<IReadOnlyCollection<LocalDeviceTerminal>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await ReadAllCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<LocalDeviceTerminal>> GetByLocalDeviceIdAsync(string localDeviceId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeId(localDeviceId);
        if (normalized is null)
        {
            return [];
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            return items.Where(x => string.Equals(x.LocalDeviceId, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalDeviceTerminal> UpsertAsync(LocalDeviceTerminal terminal, CancellationToken cancellationToken)
    {
        var normalized = NormalizeForWrite(terminal, DateTimeOffset.UtcNow);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var index = items.FindIndex(x => string.Equals(x.SourceReference, normalized.SourceReference, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                items.Add(normalized);
            }
            else
            {
                var current = items[index];
                normalized.Id = current.Id;
                normalized.CreatedAt = current.CreatedAt;
                items[index] = normalized;
            }

            await WriteAllCoreAsync(items, cancellationToken);
            return Clone(normalized);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<LocalDeviceTerminal>> ReconcileAsync(string localDeviceId, IReadOnlyCollection<LocalDeviceTerminal> discovered, CancellationToken cancellationToken)
    {
        var normalizedLocalDeviceId = NormalizeId(localDeviceId) ?? throw new InvalidOperationException("Local cihaz ID zorunludur.");
        var discoveryItems = (discovered ?? []).Select(x => NormalizeForWrite(x, DateTimeOffset.UtcNow, normalizedLocalDeviceId)).ToList();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var existingForDevice = items.Where(x => string.Equals(x.LocalDeviceId, normalizedLocalDeviceId, StringComparison.OrdinalIgnoreCase)).ToList();
            var existingBySource = existingForDevice.ToDictionary(x => x.SourceReference, StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var discoveredTerminal in discoveryItems)
            {
                if (!seen.Add(discoveredTerminal.SourceReference))
                {
                    continue;
                }

                if (existingBySource.TryGetValue(discoveredTerminal.SourceReference, out var current))
                {
                    current.LocalDeviceId = normalizedLocalDeviceId;
                    current.AcquirerId = discoveredTerminal.AcquirerId;
                    current.AcquirerName = discoveredTerminal.AcquirerName;
                    current.TerminalId = discoveredTerminal.TerminalId;
                    current.MerchantId = discoveredTerminal.MerchantId;
                    current.SourceReference = discoveredTerminal.SourceReference;
                    current.Active = true;
                    current.LastDiscoveredAt = now;
                    current.UpdatedAt = now;
                }
                else
                {
                    items.Add(new LocalDeviceTerminal
                    {
                        Id = discoveredTerminal.Id,
                        LocalDeviceId = normalizedLocalDeviceId,
                        AcquirerId = discoveredTerminal.AcquirerId,
                        AcquirerName = discoveredTerminal.AcquirerName,
                        TerminalId = discoveredTerminal.TerminalId,
                        MerchantId = discoveredTerminal.MerchantId,
                        SourceReference = discoveredTerminal.SourceReference,
                        Active = true,
                        LastDiscoveredAt = now,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            foreach (var current in existingForDevice.Where(x => !seen.Contains(x.SourceReference)))
            {
                current.Active = false;
                current.UpdatedAt = now;
            }

            await WriteAllCoreAsync(items, cancellationToken);
            return items.Where(x => string.Equals(x.LocalDeviceId, normalizedLocalDeviceId, StringComparison.OrdinalIgnoreCase)).Select(Clone).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteByLocalDeviceIdAsync(string localDeviceId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeId(localDeviceId) ?? throw new InvalidOperationException("Local cihaz ID zorunludur.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var removed = items.RemoveAll(x => string.Equals(x.LocalDeviceId, normalized, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                await WriteAllCoreAsync(items, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<LocalDeviceTerminal>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.LocalDeviceTerminalsStorePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_paths.LocalDeviceTerminalsStorePath);
            var items = await JsonSerializer.DeserializeAsync<List<LocalDeviceTerminal>>(stream, JsonOptions, cancellationToken);
            return items ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local device terminal store could not be read; empty list will be used.");
            return [];
        }
    }

    private async Task WriteAllCoreAsync(IReadOnlyCollection<LocalDeviceTerminal> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var tempPath = Path.Combine(_paths.DataDirectory, $"{Path.GetRandomFileName()}.tmp");

        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        ReplaceAtomically(tempPath, _paths.LocalDeviceTerminalsStorePath);
    }

    private static LocalDeviceTerminal NormalizeForWrite(LocalDeviceTerminal terminal, DateTimeOffset now, string? forcedLocalDeviceId = null)
    {
        if (terminal is null)
        {
            throw new InvalidOperationException("Terminal bilgisi zorunludur.");
        }

        var localDeviceId = NormalizeId(forcedLocalDeviceId ?? terminal.LocalDeviceId) ?? throw new InvalidOperationException("Local cihaz ID zorunludur.");
        var terminalId = NormalizeText(terminal.TerminalId) ?? throw new InvalidOperationException("TerminalId zorunludur.");
        var sourceReference = NormalizeText(terminal.SourceReference) ?? BuildSourceReference(localDeviceId, terminal.AcquirerId, terminalId);

        return new LocalDeviceTerminal
        {
            Id = NormalizeId(terminal.Id) ?? Guid.NewGuid().ToString("N"),
            LocalDeviceId = localDeviceId,
            AcquirerId = NormalizeText(terminal.AcquirerId),
            AcquirerName = NormalizeText(terminal.AcquirerName),
            TerminalId = terminalId,
            MerchantId = NormalizeText(terminal.MerchantId),
            SourceReference = sourceReference,
            Active = terminal.Active,
            LastDiscoveredAt = terminal.LastDiscoveredAt ?? now,
            CreatedAt = terminal.CreatedAt == default ? now : terminal.CreatedAt,
            UpdatedAt = now
        };
    }

    private static string BuildSourceReference(string localDeviceId, string? acquirerId, string terminalId) =>
        $"{localDeviceId.Trim()}::{NormalizeText(acquirerId) ?? string.Empty}::{terminalId.Trim()}";

    private static string? NormalizeId(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static LocalDeviceTerminal Clone(LocalDeviceTerminal terminal) => new()
    {
        Id = terminal.Id,
        LocalDeviceId = terminal.LocalDeviceId,
        AcquirerId = terminal.AcquirerId,
        AcquirerName = terminal.AcquirerName,
        TerminalId = terminal.TerminalId,
        MerchantId = terminal.MerchantId,
        SourceReference = terminal.SourceReference,
        Active = terminal.Active,
        LastDiscoveredAt = terminal.LastDiscoveredAt,
        CreatedAt = terminal.CreatedAt,
        UpdatedAt = terminal.UpdatedAt
    };

    private static void ReplaceAtomically(string tempPath, string targetPath)
    {
        try
        {
            File.Move(tempPath, targetPath, overwrite: true);
        }
        catch (IOException)
        {
            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); } catch { }
            }

            File.Move(tempPath, targetPath, overwrite: true);
        }
    }
}
