using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Modules.Pavo;

namespace STYS.Agent.LocalDevices;

public sealed class LocalDeviceManagementService : ILocalDeviceManagementService
{
    private readonly ILocalDeviceStore _store;
    private readonly ILocalDeviceTerminalStore _terminalStore;
    private readonly ILocalDeviceConnectionTesterRegistry _testerRegistry;
    private readonly IPavoLocalPairingStore _pairingStore;
    private readonly IPavoRestClient _pavoRestClient;

    public LocalDeviceManagementService(
        ILocalDeviceStore store,
        ILocalDeviceTerminalStore terminalStore,
        ILocalDeviceConnectionTesterRegistry testerRegistry,
        IPavoLocalPairingStore pairingStore,
        IPavoRestClient pavoRestClient)
    {
        _store = store;
        _terminalStore = terminalStore;
        _testerRegistry = testerRegistry;
        _pairingStore = pairingStore;
        _pavoRestClient = pavoRestClient;
    }

    public Task<IReadOnlyCollection<LocalDevice>> GetAllAsync(CancellationToken cancellationToken) =>
        _store.GetAllAsync(cancellationToken);

    public Task<LocalDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        _store.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyCollection<LocalDeviceTerminal>> GetTerminalsAsync(string localDeviceId, CancellationToken cancellationToken) =>
        _terminalStore.GetByLocalDeviceIdAsync(localDeviceId, cancellationToken);

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
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        return await TestCoreAsync(device, persistResult: true, cancellationToken);
    }

    public async Task<LocalDevice> GetDeviceInfoAsync(string id, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Cihaz bilgisi");

        var execution = await ExecuteDeviceInfoAsync(device, requirePairing: false, cancellationToken);
        return execution.UpdatedDevice;
    }

    public async Task<IReadOnlyCollection<LocalDeviceTerminal>> DiscoverTerminalsAsync(string id, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Terminal discovery");

        var execution = await ExecuteDeviceInfoAsync(device, requirePairing: true, cancellationToken);
        var terminals = MapDiscoveredTerminals(device.Id, execution.Response.Terminals);
        return await _terminalStore.ReconcileAsync(device.Id, terminals, cancellationToken);
    }

    public async Task<PavoDeviceProvisioningCandidate> BuildProvisioningCandidateAsync(string id, int tesisId, AgentSelfDto agentSelf, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Provisioning preview");

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (pairingState is null || pairingState.PairingStatus != LocalDevicePairingStatus.Paired)
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }

        ValidateTesisSelection(agentSelf, tesisId);

        var terminals = await _terminalStore.GetByLocalDeviceIdAsync(device.Id, cancellationToken);
        return new PavoDeviceProvisioningCandidate
        {
            LocalDeviceId = device.Id,
            Provider = "PAVO",
            DisplayName = device.DisplayName,
            Host = device.Host,
            HttpPort = device.HttpPort,
            HttpsPort = device.HttpsPort,
            Protocol = device.Protocol == LocalDeviceProtocol.Https ? "HTTPS" : "HTTP",
            SerialNumber = device.SerialNumber,
            DeviceName = device.DeviceName,
            PairedAt = pairingState.PairingAt,
            TesisId = tesisId,
            Terminals = terminals
                .OrderByDescending(x => x.Active)
                .ThenBy(x => x.AcquirerName)
                .ThenBy(x => x.TerminalId, StringComparer.OrdinalIgnoreCase)
                .Select(MapCandidateTerminal)
                .ToList()
        };
    }

    private async Task<DeviceInfoExecution> ExecuteDeviceInfoAsync(LocalDevice device, bool requirePairing, CancellationToken cancellationToken)
    {
        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (requirePairing)
        {
            EnsurePaired(pairingState);
        }

        var connection = await TestCoreAsync(device, persistResult: true, cancellationToken);
        if (!connection.Success)
        {
            throw new InvalidOperationException(connection.Message);
        }

        var reserved = await _pairingStore.ReserveNextTransactionSequenceAsync(device.Id, cancellationToken);
        var response = await _pavoRestClient.GetDeviceInfoAsync(BuildGetDeviceInfoRequest(device, pairingState, reserved.TransactionSequence), cancellationToken);

        if (response.HasError || response.HasAbondon || !string.IsNullOrWhiteSpace(response.ErrorCode))
        {
            throw new InvalidOperationException(response.Message ?? response.ErrorCode ?? "Cihaz bilgisi alınamadı.");
        }

        var now = DateTimeOffset.UtcNow;
        var updatedDevice = CloneForPublicUpdate(device);
        updatedDevice.SerialNumber = string.IsNullOrWhiteSpace(response.SerialNumber) ? updatedDevice.SerialNumber : response.SerialNumber.Trim();
        updatedDevice.DeviceName = string.IsNullOrWhiteSpace(response.DeviceName) ? updatedDevice.DeviceName : response.DeviceName.Trim();
        updatedDevice.LastDeviceInfoAt = now;
        updatedDevice.UpdatedAt = now;

        await _store.UpdateAsync(updatedDevice, cancellationToken);

        if (!string.IsNullOrWhiteSpace(response.Fingerprint) || !string.IsNullOrWhiteSpace(response.TargetFingerprint) || pairingState is not null)
        {
            await _pairingStore.UpsertAsync(new PavoLocalPairingState
            {
                DeviceId = updatedDevice.Id,
                Fingerprint = string.IsNullOrWhiteSpace(response.Fingerprint) ? pairingState?.Fingerprint : response.Fingerprint.Trim(),
                TargetFingerprint = string.IsNullOrWhiteSpace(response.TargetFingerprint) ? pairingState?.TargetFingerprint : response.TargetFingerprint.Trim(),
                TransactionSequence = response.TransactionHandle.TransactionSequence > 0 ? response.TransactionHandle.TransactionSequence : reserved.TransactionSequence,
                PairingStatus = pairingState?.PairingStatus ?? LocalDevicePairingStatus.NotPaired,
                PairingAt = pairingState?.PairingAt,
                LastPairingAttemptAt = pairingState?.LastPairingAttemptAt,
                LastPairingError = pairingState?.LastPairingError,
                LastDeviceInfoAt = now,
                UpdatedAt = now
            }, cancellationToken);
        }

        return new DeviceInfoExecution(updatedDevice, response, pairingState, reserved.TransactionSequence);
    }

    private static IReadOnlyCollection<LocalDeviceTerminal> MapDiscoveredTerminals(string localDeviceId, IReadOnlyCollection<PavoDeviceTerminalInfo>? terminals)
    {
        if (terminals is null || terminals.Count == 0)
        {
            return [];
        }

        return terminals
            .Where(x => !string.IsNullOrWhiteSpace(x.TerminalId))
            .Select(x =>
            {
                var terminalId = x.TerminalId.Trim();
                var acquirerId = NormalizeText(x.AcquirerId);
                var sourceReference = $"{localDeviceId.Trim()}::{acquirerId ?? string.Empty}::{terminalId}";

                return new LocalDeviceTerminal
                {
                    LocalDeviceId = localDeviceId,
                    AcquirerId = acquirerId,
                    AcquirerName = NormalizeText(x.AcquirerName),
                    TerminalId = terminalId,
                    MerchantId = NormalizeText(x.MerchantId),
                    SourceReference = sourceReference,
                    Active = true
                };
            })
            .ToList();
    }

    private static PavoDeviceProvisioningCandidateTerminal MapCandidateTerminal(LocalDeviceTerminal terminal) => new()
    {
        AcquirerId = terminal.AcquirerId,
        AcquirerName = terminal.AcquirerName,
        TerminalId = terminal.TerminalId,
        MerchantId = terminal.MerchantId,
        SourceReference = terminal.SourceReference,
        Active = terminal.Active,
        LastDiscoveredAt = terminal.LastDiscoveredAt
    };

    private static void ValidateTesisSelection(AgentSelfDto agentSelf, int tesisId)
    {
        if (agentSelf is null)
        {
            throw new InvalidOperationException("Agent bilgisi alınamadı.");
        }

        if (tesisId <= 0)
        {
            throw new InvalidOperationException("Tesis seçimi zorunludur.");
        }

        var allowed = agentSelf.Tesisler?.Any(x => x.Id == tesisId) == true;
        if (!allowed)
        {
            throw new InvalidOperationException("Seçilen tesis mevcut agent kapsamı içinde değil.");
        }
    }

    private static void EnsurePaired(PavoLocalPairingState? pairingState)
    {
        if (pairingState is null || pairingState.PairingStatus != LocalDevicePairingStatus.Paired)
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }
    }

    public async Task<LocalDevice> PairAsync(string id, bool forceRePair, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Pairing");

        if (device.PairingStatus == LocalDevicePairingStatus.Paired && !forceRePair)
        {
            throw new InvalidOperationException("Bu cihaz zaten eşleştirilmiş. Yeniden pairing mevcut pairing bilgisini değiştirebilir.");
        }

        var connection = await TestCoreAsync(device, persistResult: true, cancellationToken);
        if (!connection.Success)
        {
            throw new InvalidOperationException(connection.Message);
        }

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (pairingState is null || string.IsNullOrWhiteSpace(pairingState.Fingerprint))
        {
            throw new InvalidOperationException("Önce Cihaz Bilgisini Getir işlemi tamamlanmalıdır.");
        }

        var reserved = await _pairingStore.ReserveNextTransactionSequenceAsync(device.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var request = BuildPairingRequest(device, pairingState, reserved.TransactionSequence);

        try
        {
            var response = await _pavoRestClient.PairingAsync(request, cancellationToken);
            if (response.HasError || response.HasAbondon || !string.IsNullOrWhiteSpace(response.ErrorCode) || !response.OnayliMi)
            {
                await RecordPairingFailureAsync(device, pairingState, reserved.TransactionSequence, response.Message ?? response.ErrorCode ?? "Pairing başarısız.", now, cancellationToken);
                throw new InvalidOperationException(response.Message ?? response.ErrorCode ?? "Pairing başarısız.");
            }

            var successState = new PavoLocalPairingState
            {
                DeviceId = device.Id,
                Fingerprint = string.IsNullOrWhiteSpace(response.Fingerprint) ? pairingState.Fingerprint : response.Fingerprint.Trim(),
                TargetFingerprint = string.IsNullOrWhiteSpace(response.TargetFingerprint) ? pairingState.TargetFingerprint : response.TargetFingerprint.Trim(),
                TransactionSequence = response.TransactionHandle.TransactionSequence > 0 ? response.TransactionHandle.TransactionSequence : reserved.TransactionSequence,
                PairingStatus = LocalDevicePairingStatus.Paired,
                PairingAt = now,
                LastPairingAttemptAt = now,
                LastPairingError = null,
                LastDeviceInfoAt = pairingState.LastDeviceInfoAt,
                UpdatedAt = now
            };

            await _pairingStore.UpsertAsync(successState, cancellationToken);

            var updatedDevice = CloneForPublicUpdate(device);
            updatedDevice.PairingStatus = LocalDevicePairingStatus.Paired;
            updatedDevice.LastPairingAt = now;
            updatedDevice.LastPairingAttemptAt = now;
            updatedDevice.LastPairingError = null;
            updatedDevice.UpdatedAt = now;
            await _store.UpdateAsync(updatedDevice, cancellationToken);
            return updatedDevice;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await RecordPairingFailureAsync(device, pairingState, reserved.TransactionSequence, ex.Message, now, cancellationToken);
            throw;
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _store.DeleteAsync(id, cancellationToken);
        await _pairingStore.DeleteAsync(id, cancellationToken);
        await _terminalStore.DeleteByLocalDeviceIdAsync(id, cancellationToken);
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

    private async Task<LocalDevice> GetDeviceOrThrowAsync(string id, CancellationToken cancellationToken) =>
        await _store.GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Local cihaz bulunamadı.");

    private static void ValidatePavoLocalDevice(LocalDevice device, string operationName)
    {
        if (device.Provider is not LocalDeviceProvider.Pavo)
        {
            throw new InvalidOperationException($"{operationName} yalnızca PAVO provider için destekleniyor.");
        }

        if (device.DeviceType is not LocalDeviceType.Pos)
        {
            throw new InvalidOperationException("PAVO local discovery ve pairing yalnızca POS cihazlarında destekleniyor.");
        }
    }

    private static LocalDevice BuildDevice(NormalizedUpsert normalized, LocalDevice? existing)
    {
        var createdAt = existing?.CreatedAt ?? DateTimeOffset.UtcNow;
        var status = existing?.Status ?? LocalDeviceConnectionStatus.Unknown;
        var lastTestAt = existing?.LastConnectionTestAt;
        var lastSuccess = existing?.LastConnectionSuccess;
        var lastError = existing?.LastError;
        var pairingStatus = existing?.PairingStatus ?? LocalDevicePairingStatus.NotPaired;
        var lastDeviceInfoAt = existing?.LastDeviceInfoAt;
        var lastPairingAttemptAt = existing?.LastPairingAttemptAt;
        var lastPairingAt = existing?.LastPairingAt;
        var lastPairingError = existing?.LastPairingError;
        var deviceName = existing?.DeviceName;

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
            DeviceName = deviceName,
            Status = status,
            LastConnectionTestAt = lastTestAt,
            LastConnectionSuccess = lastSuccess,
            LastError = lastError,
            PairingStatus = pairingStatus,
            LastDeviceInfoAt = lastDeviceInfoAt,
            LastPairingAttemptAt = lastPairingAttemptAt,
            LastPairingAt = lastPairingAt,
            LastPairingError = lastPairingError,
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
            PairingStatus = LocalDevicePairingStatus.NotPaired,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PavoGetDeviceInfoRequest BuildGetDeviceInfoRequest(LocalDevice device, PavoLocalPairingState? state, long transactionSequence) => new()
    {
        PosCihaziId = 0,
        IpAddress = device.Host,
        HttpPort = device.Protocol == LocalDeviceProtocol.Http ? device.HttpPort : null,
        HttpsPort = device.Protocol == LocalDeviceProtocol.Https ? device.HttpsPort : null,
        UseHttps = device.Protocol == LocalDeviceProtocol.Https,
        TransactionHandle = new PavoTransactionHandle
        {
            SerialNumber = device.SerialNumber ?? string.Empty,
            Fingerprint = state?.Fingerprint ?? string.Empty,
            TransactionSequence = transactionSequence,
            TransactionDate = DateTime.UtcNow
        }
    };

    private static PavoPairingRequest BuildPairingRequest(LocalDevice device, PavoLocalPairingState pairingState, long transactionSequence) => new()
    {
        PosCihaziId = 0,
        IpAddress = device.Host,
        HttpPort = device.Protocol == LocalDeviceProtocol.Http ? device.HttpPort : null,
        HttpsPort = device.Protocol == LocalDeviceProtocol.Https ? device.HttpsPort : null,
        UseHttps = device.Protocol == LocalDeviceProtocol.Https,
        CurrentFingerprint = pairingState.Fingerprint,
        TransactionHandle = new PavoTransactionHandle
        {
            SerialNumber = device.SerialNumber ?? string.Empty,
            Fingerprint = pairingState.Fingerprint ?? string.Empty,
            TransactionSequence = transactionSequence,
            TransactionDate = DateTime.UtcNow
        }
    };

    private async Task RecordPairingFailureAsync(
        LocalDevice device,
        PavoLocalPairingState pairingState,
        long reservedSequence,
        string errorMessage,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var hadSuccessfulPairing = device.PairingStatus == LocalDevicePairingStatus.Paired || pairingState.PairingStatus == LocalDevicePairingStatus.Paired;
        var updatedState = new PavoLocalPairingState
        {
            DeviceId = device.Id,
            Fingerprint = pairingState.Fingerprint,
            TargetFingerprint = pairingState.TargetFingerprint,
            TransactionSequence = Math.Max(pairingState.TransactionSequence, reservedSequence),
            PairingStatus = hadSuccessfulPairing ? LocalDevicePairingStatus.Paired : LocalDevicePairingStatus.Failed,
            PairingAt = pairingState.PairingAt,
            LastPairingAttemptAt = timestamp,
            LastPairingError = errorMessage,
            LastDeviceInfoAt = pairingState.LastDeviceInfoAt,
            UpdatedAt = timestamp
        };

        await _pairingStore.UpsertAsync(updatedState, cancellationToken);

        var updatedDevice = CloneForPublicUpdate(device);
        updatedDevice.PairingStatus = hadSuccessfulPairing ? LocalDevicePairingStatus.Paired : LocalDevicePairingStatus.Failed;
        updatedDevice.LastPairingAttemptAt = timestamp;
        updatedDevice.LastPairingError = errorMessage;
        if (hadSuccessfulPairing)
        {
            updatedDevice.LastPairingAt = device.LastPairingAt ?? pairingState.PairingAt;
        }

        updatedDevice.UpdatedAt = timestamp;
        await _store.UpdateAsync(updatedDevice, cancellationToken);
    }

    private static LocalDevice CloneForPublicUpdate(LocalDevice device) => new()
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
        DeviceName = device.DeviceName,
        Status = device.Status,
        LastConnectionTestAt = device.LastConnectionTestAt,
        LastConnectionSuccess = device.LastConnectionSuccess,
        LastError = device.LastError,
        PairingStatus = device.PairingStatus,
        LastDeviceInfoAt = device.LastDeviceInfoAt,
        LastPairingAttemptAt = device.LastPairingAttemptAt,
        LastPairingAt = device.LastPairingAt,
        LastPairingError = device.LastPairingError,
        CreatedAt = device.CreatedAt,
        UpdatedAt = device.UpdatedAt
    };

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

    private static string? NormalizeText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
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

    private sealed record DeviceInfoExecution(
        LocalDevice UpdatedDevice,
        PavoGetDeviceInfoResponse Response,
        PavoLocalPairingState? PairingState,
        long ReservedTransactionSequence);
}
