namespace STYS.Agent.LocalDevices;

public sealed class LocalDeviceManagementService : ILocalDeviceManagementService
{
    private readonly ILocalDeviceStore _store;
    private readonly ILocalDeviceConnectionTesterRegistry _testerRegistry;

    public LocalDeviceManagementService(
        ILocalDeviceStore store,
        ILocalDeviceConnectionTesterRegistry testerRegistry)
    {
        _store = store;
        _testerRegistry = testerRegistry;
    }

    public Task<IReadOnlyCollection<LocalDevice>> GetAllAsync(CancellationToken cancellationToken) =>
        _store.GetAllAsync(cancellationToken);

    public Task<LocalDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _store.GetByIdAsync(id, cancellationToken);

    public async Task<LocalDevice> SaveAsync(LocalDeviceUpsertRequest request, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var existing = normalized.IsNew
            ? null
            : await _store.GetByIdAsync(normalized.Id!, cancellationToken);

        if (!normalized.IsNew && existing is null)
        {
            throw new InvalidOperationException("Local cihaz bulunamadı.");
        }

        var device = BuildDevice(normalized, existing);

        return existing is null
            ? await _store.CreateAsync(device, cancellationToken)
            : await _store.UpdateAsync(device, cancellationToken);
    }

    public async Task<LocalDeviceConnectionTestResult> TestAsync(LocalDeviceTestRequest request, CancellationToken cancellationToken)
    {
        var device = BuildTransientDevice(request);
        return await TestCoreAsync(device, persistResult: false, cancellationToken);
    }

    public async Task<LocalDeviceConnectionTestResult> TestAsync(string id, CancellationToken cancellationToken)
    {
        var device = await _store.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Local cihaz bulunamadı.");

        return await TestCoreAsync(device, persistResult: true, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _store.DeleteAsync(id, cancellationToken);
    }

    private async Task<LocalDeviceConnectionTestResult> TestCoreAsync(LocalDevice device, bool persistResult, CancellationToken cancellationToken)
    {
        var tester = ResolveTester(device.Provider);
        var result = await tester.TestAsync(device, cancellationToken);

        if (persistResult)
        {
            device.Status = result.Status;
            device.LastConnectionSuccess = result.Success;
            device.LastConnectionTestAt = result.TestedAt;
            device.LastError = result.Success ? null : result.Message;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpdateAsync(device, cancellationToken);
        }

        return result;
    }

    private ILocalDeviceConnectionTester ResolveTester(LocalDeviceProvider provider)
    {
        if (_testerRegistry.TryGetTester(provider, out var tester))
        {
            return tester;
        }

        throw new InvalidOperationException($"{provider} sağlayıcısı için connection tester tanımlı değil.");
    }

    private static LocalDevice BuildDevice(NormalizedUpsert normalized, LocalDevice? existing)
    {
        var createdAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow;
        var status = existing?.Status ?? LocalDeviceConnectionStatus.Unknown;
        var lastTestAt = existing?.LastConnectionTestAt;
        var lastSuccess = existing?.LastConnectionSuccess;
        var lastError = existing?.LastError;

        return new LocalDevice
        {
            Id = normalized.Id!,
            DeviceType = normalized.DeviceType,
            Provider = normalized.Provider,
            DisplayName = normalized.DisplayName,
            Host = normalized.Host,
            HttpPort = normalized.HttpPort,
            HttpsPort = normalized.HttpsPort,
            Protocol = normalized.Protocol,
            SerialNumber = normalized.SerialNumber,
            Status = status,
            LastConnectionTestAt = lastTestAt,
            LastConnectionSuccess = lastSuccess,
            LastError = lastError,
            CreatedAt = createdAt,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static LocalDevice BuildTransientDevice(LocalDeviceTestRequest request)
    {
        var normalized = Normalize(request);
        return new LocalDevice
        {
            Id = string.Empty,
            DeviceType = normalized.DeviceType,
            Provider = normalized.Provider,
            DisplayName = normalized.DisplayName,
            Host = normalized.Host,
            HttpPort = normalized.HttpPort,
            HttpsPort = normalized.HttpsPort,
            Protocol = normalized.Protocol,
            SerialNumber = normalized.SerialNumber,
            Status = LocalDeviceConnectionStatus.Unknown,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static NormalizedUpsert Normalize(LocalDeviceUpsertRequest request) =>
        NormalizeCore(
            request?.Id,
            request?.DisplayName,
            request?.Host,
            request?.HttpPort,
            request?.HttpsPort,
            request?.Protocol ?? default,
            request?.DeviceType ?? default,
            request?.Provider ?? default,
            request?.SerialNumber,
            isNew: string.IsNullOrWhiteSpace(request?.Id));

    private static NormalizedUpsert Normalize(LocalDeviceTestRequest request) =>
        NormalizeCore(
            null,
            request?.DisplayName,
            request?.Host,
            request?.HttpPort,
            request?.HttpsPort,
            request?.Protocol ?? default,
            request?.DeviceType ?? default,
            request?.Provider ?? default,
            request?.SerialNumber,
            isNew: true);

    private static NormalizedUpsert NormalizeCore(
        string? id,
        string? displayName,
        string? host,
        int? httpPort,
        int? httpsPort,
        LocalDeviceProtocol protocol,
        LocalDeviceType deviceType,
        LocalDeviceProvider provider,
        string? serialNumber,
        bool isNew)
    {
        if (provider is not LocalDeviceProvider.Pavo)
        {
            throw new InvalidOperationException("Şu anda yalnızca PAVO provider destekleniyor.");
        }

        if (deviceType is not LocalDeviceType.Pos and not LocalDeviceType.Printer)
        {
            throw new InvalidOperationException("Geçersiz cihaz tipi.");
        }

        if (protocol is not LocalDeviceProtocol.Http and not LocalDeviceProtocol.Https)
        {
            throw new InvalidOperationException("Geçersiz protocol.");
        }

        var normalizedId = string.IsNullOrWhiteSpace(id) ? (isNew ? null : throw new InvalidOperationException("Cihaz Id zorunludur.")) : id.Trim();
        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
        {
            throw new InvalidOperationException("Cihaz adı zorunludur.");
        }

        var normalizedHost = NormalizeHost(host);
        var normalizedHttpPort = NormalizePort(httpPort, 4567);
        var normalizedHttpsPort = NormalizePort(httpsPort, 4568);

        return new NormalizedUpsert(
            normalizedId ?? Guid.NewGuid().ToString("N"),
            normalizedDisplayName,
            normalizedHost,
            normalizedHttpPort,
            normalizedHttpsPort,
            protocol,
            deviceType,
            provider,
            string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            isNew);
    }

    private static string NormalizeHost(string? host)
    {
        var value = host?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Host/IP zorunludur.");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Host yalnızca düz host veya IP değeri olmalıdır.");
        }

        if (value.Contains("://", StringComparison.Ordinal) || value.Contains('/') || value.Contains('\\'))
        {
            throw new InvalidOperationException("Host yalnızca düz host veya IP değeri olmalıdır.");
        }

        var hostType = Uri.CheckHostName(value);
        if (hostType is UriHostNameType.Unknown)
        {
            if (!System.Net.IPAddress.TryParse(value, out _))
            {
                throw new InvalidOperationException("Geçersiz host/IP değeri.");
            }
        }

        return value;
    }

    private static int NormalizePort(int? value, int defaultValue)
    {
        var port = value.GetValueOrDefault(defaultValue);
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Port değeri 1-65535 arasında olmalıdır.");
        }

        return port;
    }

    private sealed record NormalizedUpsert(
        string Id,
        string DisplayName,
        string Host,
        int HttpPort,
        int HttpsPort,
        LocalDeviceProtocol Protocol,
        LocalDeviceType DeviceType,
        LocalDeviceProvider Provider,
        string? SerialNumber,
        bool IsNew);
}
