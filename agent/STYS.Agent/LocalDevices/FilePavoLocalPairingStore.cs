using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Agent.LocalDevices;

public sealed class FilePavoLocalPairingStore : IPavoLocalPairingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentPathResolver _paths;
    private readonly ILogger<FilePavoLocalPairingStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FilePavoLocalPairingStore(IAgentPathResolver paths, ILogger<FilePavoLocalPairingStore> logger)
    {
        _paths = paths;
        _logger = logger;
        Directory.CreateDirectory(paths.DataDirectory);
        SecureDirectory(paths.DataDirectory);
    }

    public async Task<PavoLocalPairingState?> GetAsync(string deviceId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeId(deviceId);
        if (normalized is null)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            return items.TryGetValue(normalized, out var state) ? Clone(state) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PavoLocalPairingState> UpsertAsync(PavoLocalPairingState state, CancellationToken cancellationToken)
    {
        var normalized = Normalize(state);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            if (items.TryGetValue(normalized.DeviceId, out var existing))
            {
                items[normalized.DeviceId] = Merge(existing, normalized);
            }
            else
            {
                items[normalized.DeviceId] = normalized;
            }

            var persisted = items[normalized.DeviceId];
            await WriteAllCoreAsync(items, cancellationToken);
            return Clone(persisted);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PavoLocalPairingState> ReserveNextTransactionSequenceAsync(string deviceId, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeId(deviceId) ?? throw new InvalidOperationException("Cihaz ID zorunludur.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            if (!items.TryGetValue(normalizedId, out var state))
            {
                state = new PavoLocalPairingState
                {
                    DeviceId = normalizedId,
                    PairingStatus = LocalDevicePairingStatus.NotPaired
                };
            }

            state.TransactionSequence = Math.Max(0, state.TransactionSequence) + 1;
            state.UpdatedAt = DateTimeOffset.UtcNow;
            items[normalizedId] = state;
            await WriteAllCoreAsync(items, cancellationToken);
            return Clone(state);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string deviceId, CancellationToken cancellationToken)
    {
        var normalized = NormalizeId(deviceId) ?? throw new InvalidOperationException("Cihaz ID zorunludur.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadAllCoreAsync(cancellationToken);
            if (items.Remove(normalized))
            {
                await WriteAllCoreAsync(items, cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, PavoLocalPairingState>> ReadAllCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.PavoPairingStorePath))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var stream = File.OpenRead(_paths.PavoPairingStorePath);
            var encrypted = await JsonSerializer.DeserializeAsync<EncryptedPayload>(stream, JsonOptions, cancellationToken);
            if (encrypted?.Data is null)
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }

            var plainBytes = Unprotect(Convert.FromBase64String(encrypted.Data));
            if (plainBytes is null)
            {
                return new(StringComparer.OrdinalIgnoreCase);
            }

            var json = Encoding.UTF8.GetString(plainBytes);
            var items = JsonSerializer.Deserialize<Dictionary<string, PavoLocalPairingState>>(json, JsonOptions);
            return items is null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, PavoLocalPairingState>(items, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local PAVO pairing store could not be read; empty state will be used.");
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task WriteAllCoreAsync(Dictionary<string, PavoLocalPairingState> items, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var tempPath = Path.Combine(_paths.DataDirectory, $"{Path.GetRandomFileName()}.tmp");
        var json = JsonSerializer.Serialize(items, JsonOptions);
        var encrypted = Protect(Encoding.UTF8.GetBytes(json));
        var payload = new EncryptedPayload { Data = Convert.ToBase64String(encrypted) };

        await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        ReplaceAtomically(tempPath, _paths.PavoPairingStorePath);

        SecureFile(_paths.PavoPairingStorePath);
    }

    private static PavoLocalPairingState Normalize(PavoLocalPairingState state)
    {
        if (state is null)
        {
            throw new InvalidOperationException("Pairing state zorunludur.");
        }

        var id = NormalizeId(state.DeviceId) ?? throw new InvalidOperationException("Cihaz ID zorunludur.");

        return new PavoLocalPairingState
        {
            DeviceId = id,
            Fingerprint = string.IsNullOrWhiteSpace(state.Fingerprint) ? null : state.Fingerprint.Trim(),
            TargetFingerprint = string.IsNullOrWhiteSpace(state.TargetFingerprint) ? null : state.TargetFingerprint.Trim(),
            TransactionSequence = Math.Max(0, state.TransactionSequence),
            PairingStatus = state.PairingStatus,
            PairingAt = state.PairingAt,
            LastPairingAttemptAt = state.LastPairingAttemptAt,
            LastPairingError = string.IsNullOrWhiteSpace(state.LastPairingError) ? null : state.LastPairingError.Trim(),
            LastDeviceInfoAt = state.LastDeviceInfoAt,
            UpdatedAt = state.UpdatedAt == default ? DateTimeOffset.UtcNow : state.UpdatedAt
        };
    }

    private static PavoLocalPairingState Merge(PavoLocalPairingState existing, PavoLocalPairingState incoming)
    {
        var existingSequence = Math.Max(0, existing.TransactionSequence);
        var incomingSequence = Math.Max(0, incoming.TransactionSequence);
        var preferIncoming = incomingSequence >= existingSequence;

        return new PavoLocalPairingState
        {
            DeviceId = existing.DeviceId,
            Fingerprint = MergeText(existing.Fingerprint, incoming.Fingerprint, preferIncoming),
            TargetFingerprint = MergeText(existing.TargetFingerprint, incoming.TargetFingerprint, preferIncoming),
            TransactionSequence = Math.Max(existingSequence, incomingSequence),
            PairingStatus = MergePairingStatus(existing.PairingStatus, incoming.PairingStatus, preferIncoming),
            PairingAt = MergeDateTime(existing.PairingAt, incoming.PairingAt, preferIncoming),
            LastPairingAttemptAt = MergeDateTime(existing.LastPairingAttemptAt, incoming.LastPairingAttemptAt, preferIncoming),
            LastPairingError = MergeText(existing.LastPairingError, incoming.LastPairingError, preferIncoming),
            LastDeviceInfoAt = MergeDateTime(existing.LastDeviceInfoAt, incoming.LastDeviceInfoAt, preferIncoming),
            UpdatedAt = preferIncoming && incoming.UpdatedAt != default && incoming.UpdatedAt >= existing.UpdatedAt
                ? incoming.UpdatedAt
                : existing.UpdatedAt
        };
    }

    private static string? MergeText(string? existing, string? incoming, bool preferIncoming)
    {
        var existingValue = string.IsNullOrWhiteSpace(existing) ? null : existing.Trim();
        var incomingValue = string.IsNullOrWhiteSpace(incoming) ? null : incoming.Trim();

        if (preferIncoming)
        {
            return incomingValue ?? existingValue;
        }

        return existingValue ?? incomingValue;
    }

    private static DateTimeOffset? MergeDateTime(DateTimeOffset? existing, DateTimeOffset? incoming, bool preferIncoming)
    {
        if (preferIncoming)
        {
            return incoming ?? existing;
        }

        return existing ?? incoming;
    }

    private static LocalDevicePairingStatus MergePairingStatus(LocalDevicePairingStatus existing, LocalDevicePairingStatus incoming, bool preferIncoming)
    {
        if (preferIncoming)
        {
            return incoming;
        }

        return existing == LocalDevicePairingStatus.Paired ? existing : incoming;
    }

    private static PavoLocalPairingState Clone(PavoLocalPairingState state) => new()
    {
        DeviceId = state.DeviceId,
        Fingerprint = state.Fingerprint,
        TargetFingerprint = state.TargetFingerprint,
        TransactionSequence = state.TransactionSequence,
        PairingStatus = state.PairingStatus,
        PairingAt = state.PairingAt,
        LastPairingAttemptAt = state.LastPairingAttemptAt,
        LastPairingError = state.LastPairingError,
        LastDeviceInfoAt = state.LastDeviceInfoAt,
        UpdatedAt = state.UpdatedAt
    };

    private static string? NormalizeId(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static byte[] Protect(byte[] data)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }

        return data;
    }

    private static byte[]? Unprotect(byte[] data)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

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

    private static void ReplaceAtomically(string tempPath, string targetPath)
    {
        try
        {
            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }

            File.Move(tempPath, targetPath);
            return;
        }
        catch (IOException) when (File.Exists(tempPath))
        {
            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }

            File.Move(tempPath, targetPath);
        }
        catch (UnauthorizedAccessException) when (File.Exists(tempPath))
        {
            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                return;
            }

            File.Move(tempPath, targetPath);
        }
    }

    private sealed class EncryptedPayload
    {
        public string? Data { get; set; }
    }
}
