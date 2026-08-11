using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.LocalDevices;

public sealed class FileLocalDeviceStore : ILocalDeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentPathResolver _paths;
    private readonly ILogger<FileLocalDeviceStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileLocalDeviceStore(IAgentPathResolver paths, ILogger<FileLocalDeviceStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<LocalDevice>> GetAllAsync(CancellationToken cancellationToken)
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

    public async Task<LocalDevice?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeId(id);
        if (normalizedId is null)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            return items.FirstOrDefault(x => string.Equals(x.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalDevice> CreateAsync(LocalDevice device, CancellationToken cancellationToken)
    {
        var normalized = NormalizeForWrite(device, isNew: true);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            if (items.Any(x => string.Equals(x.Id, normalized.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Aynı Id ile bir local cihaz zaten kayıtlı.");
            }

            items.Add(normalized);
            await WriteAllCoreAsync(items, cancellationToken);
            return Clone(normalized);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalDevice> UpdateAsync(LocalDevice device, CancellationToken cancellationToken)
    {
        var normalized = NormalizeForWrite(device, isNew: false);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var index = items.FindIndex(x => string.Equals(x.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException("Local cihaz bulunamadı.");
            }

            items[index] = normalized;
            await WriteAllCoreAsync(items, cancellationToken);
            return Clone(normalized);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeId(id) ?? throw new InvalidOperationException("Cihaz ID zorunludur.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = (await ReadAllCoreAsync(cancellationToken)).ToList();
            var removed = items.RemoveAll(x => string.Equals(x.Id, normalizedId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                throw new InvalidOperationException("Local cihaz bulunamadı.");
            }

            await WriteAllCoreAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<LocalDevice>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.LocalDevicesStorePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_paths.LocalDevicesStorePath);
            var devices = await JsonSerializer.DeserializeAsync<List<LocalDevice>>(stream, JsonOptions, cancellationToken);
            return devices ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local device store could not be read; empty list will be used.");
            return [];
        }
    }

    private async Task WriteAllCoreAsync(IReadOnlyCollection<LocalDevice> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var tempPath = Path.Combine(_paths.DataDirectory, $"{Path.GetRandomFileName()}.tmp");

        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, items, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        if (File.Exists(_paths.LocalDevicesStorePath))
        {
            File.Move(tempPath, _paths.LocalDevicesStorePath, true);
            return;
        }

        File.Move(tempPath, _paths.LocalDevicesStorePath);
    }

    private static LocalDevice NormalizeForWrite(LocalDevice device, bool isNew)
    {
        if (device is null)
        {
            throw new InvalidOperationException("Cihaz bilgisi zorunludur.");
        }

        var id = NormalizeId(device.Id) ?? (isNew ? Guid.NewGuid().ToString("N") : throw new InvalidOperationException("Cihaz Id zorunludur."));
        var displayName = device.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException("Cihaz adı zorunludur.");
        }

        var host = NormalizeHost(device.Host);
        var protocol = ValidateProtocol(device.Protocol);
        var httpPort = NormalizePort(device.HttpPort, 4567);
        var httpsPort = NormalizePort(device.HttpsPort, 4568);

        return new LocalDevice
        {
            Id = id,
            DeviceType = device.DeviceType,
            Provider = device.Provider,
            DisplayName = displayName,
            Host = host,
            HttpPort = httpPort,
            HttpsPort = httpsPort,
            Protocol = protocol,
            SerialNumber = string.IsNullOrWhiteSpace(device.SerialNumber) ? null : device.SerialNumber.Trim(),
            Status = device.Status,
            LastConnectionTestAt = device.LastConnectionTestAt,
            LastConnectionSuccess = device.LastConnectionSuccess,
            LastError = string.IsNullOrWhiteSpace(device.LastError) ? null : device.LastError.Trim(),
            CreatedAt = device.CreatedAt == default ? DateTimeOffset.UtcNow : device.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static string? NormalizeId(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string NormalizeHost(string host)
    {
        var value = host?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Host/IP zorunludur.");
        }

        if (value.Contains("://", StringComparison.Ordinal) || value.Contains('/') || value.Contains('\\'))
        {
            throw new InvalidOperationException("Host yalnızca düz host veya IP değeri olmalıdır.");
        }

        var hostType = Uri.CheckHostName(value);
        if (hostType is UriHostNameType.Unknown or UriHostNameType.Basic)
        {
            if (!System.Net.IPAddress.TryParse(value, out _))
            {
                throw new InvalidOperationException("Geçersiz host/IP değeri.");
            }
        }

        return value;
    }

    private static LocalDeviceProtocol ValidateProtocol(LocalDeviceProtocol protocol) =>
        protocol is LocalDeviceProtocol.Http or LocalDeviceProtocol.Https
            ? protocol
            : throw new InvalidOperationException("Geçersiz protocol.");

    private static int NormalizePort(int? value, int defaultValue)
    {
        var port = value.GetValueOrDefault(defaultValue);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Port değeri 1-65535 arasında olmalıdır.");
        }

        return port;
    }

    private static LocalDevice Clone(LocalDevice device) => new()
    {
        Id = device.Id,
        DeviceType = device.DeviceType,
        Provider = device.Provider,
        DisplayName = device.DisplayName,
        Host = device.Host,
        HttpPort = device.HttpPort,
        HttpsPort = device.HttpsPort,
        Protocol = device.Protocol,
        SerialNumber = device.SerialNumber,
        Status = device.Status,
        LastConnectionTestAt = device.LastConnectionTestAt,
        LastConnectionSuccess = device.LastConnectionSuccess,
        LastError = device.LastError,
        CreatedAt = device.CreatedAt,
        UpdatedAt = device.UpdatedAt
    };
}
