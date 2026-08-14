using STYS.Agent.Contracts.Dtos;
using STYS.Agent.Client;
using STYS.Agent.Modules.Pavo;

namespace STYS.Agent.LocalDevices;

public sealed class LocalDeviceManagementService : ILocalDeviceManagementService
{
    private readonly ILocalDeviceStore _store;
    private readonly ILocalDeviceTerminalStore _terminalStore;
    private readonly ILocalDeviceConnectionTesterRegistry _testerRegistry;
    private readonly IPavoLocalPairingStore _pairingStore;
    private readonly IPavoRestClient _pavoRestClient;
    private readonly IStysAgentApiClient? _agentApiClient;

    public LocalDeviceManagementService(
        ILocalDeviceStore store,
        ILocalDeviceTerminalStore terminalStore,
        ILocalDeviceConnectionTesterRegistry testerRegistry,
        IPavoLocalPairingStore pairingStore,
        IPavoRestClient pavoRestClient,
        IStysAgentApiClient? agentApiClient = null)
    {
        _store = store;
        _terminalStore = terminalStore;
        _testerRegistry = testerRegistry;
        _pairingStore = pairingStore;
        _pavoRestClient = pavoRestClient;
        _agentApiClient = agentApiClient;
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
        if (existing is not null
            && existing.ProvisioningStatus == LocalDeviceProvisioningStatus.Provisioned
            && HasLifecycleRelevantChanges(existing, device))
        {
            device.ProvisioningStatus = LocalDeviceProvisioningStatus.ReProvisionRequired;
            device.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.ReProvisionRequired;
            device.StysReconciliationMessage = "Yerel cihaz değişti; yeniden STYS eşitleme gerekli.";
            device.StysReconciliationCheckedAt = DateTimeOffset.UtcNow;
        }

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
        if (device.ProvisioningStatus == LocalDeviceProvisioningStatus.Provisioned
            && HasLifecycleRelevantChanges(device, execution.UpdatedDevice))
        {
            execution.UpdatedDevice.ProvisioningStatus = LocalDeviceProvisioningStatus.ReProvisionRequired;
            execution.UpdatedDevice.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.ReProvisionRequired;
            execution.UpdatedDevice.StysReconciliationMessage = "Cihaz bilgisi değişti; yeniden STYS eşitleme gerekli.";
            execution.UpdatedDevice.StysReconciliationCheckedAt = DateTimeOffset.UtcNow;
            await _store.UpdateAsync(execution.UpdatedDevice, cancellationToken);
        }

        return execution.UpdatedDevice;
    }

    public async Task<IReadOnlyCollection<LocalDeviceTerminal>> DiscoverTerminalsAsync(string id, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Terminal discovery");

        var existingTerminalKeys = (await _terminalStore.GetByLocalDeviceIdAsync(device.Id, cancellationToken))
            .Where(x => x.Active)
            .Select(BuildTerminalIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var execution = await ExecuteDeviceInfoAsync(device, requirePairing: true, cancellationToken);
        var terminals = MapDiscoveredTerminals(device.Id, execution.Response.Terminals);
        var reconciled = await _terminalStore.ReconcileAsync(device.Id, terminals, cancellationToken);

        if (device.ProvisioningStatus == LocalDeviceProvisioningStatus.Provisioned)
        {
            var currentTerminalKeys = reconciled.Where(x => x.Active).Select(BuildTerminalIdentity).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!existingTerminalKeys.SetEquals(currentTerminalKeys))
            {
                var updatedDevice = CloneForPublicUpdate(execution.UpdatedDevice);
                updatedDevice.ProvisioningStatus = LocalDeviceProvisioningStatus.ReProvisionRequired;
                updatedDevice.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.ReProvisionRequired;
                updatedDevice.StysReconciliationMessage = "Terminal topolojisi değişti; yeniden STYS eşitleme gerekli.";
                updatedDevice.StysReconciliationCheckedAt = DateTimeOffset.UtcNow;
                updatedDevice.UpdatedAt = DateTimeOffset.UtcNow;
                await _store.UpdateAsync(updatedDevice, cancellationToken);
            }
        }

        return reconciled;
    }

    public async Task<LocalDevicePaymentTestResult> StartPaymentTestAsync(string id, LocalDevicePaymentTestRequest request, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "Ödeme testi");

        if (device.ProvisioningStatus is LocalDeviceProvisioningStatus.ReProvisionRequired
            or LocalDeviceProvisioningStatus.Conflict
            or LocalDeviceProvisioningStatus.Disabled)
        {
            throw new InvalidOperationException("Bu cihaz ödeme için hazır değil.");
        }

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (pairingState is null || pairingState.PairingStatus != LocalDevicePairingStatus.Paired || string.IsNullOrWhiteSpace(pairingState.Fingerprint))
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }

        var terminals = await _terminalStore.GetByLocalDeviceIdAsync(device.Id, cancellationToken);
        var activeTerminals = terminals.Where(x => x.Active).ToList();
        if (activeTerminals.Count == 0)
        {
            throw new InvalidOperationException("Ödeme testi için en az bir aktif terminal gerekli.");
        }

        LocalDeviceTerminal selectedTerminal;
        if (!string.IsNullOrWhiteSpace(request.SelectedTerminalId))
        {
            selectedTerminal = activeTerminals.FirstOrDefault(x => string.Equals(x.TerminalId, request.SelectedTerminalId.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Seçilen terminal bulunamadı.");
        }
        else
        {
            selectedTerminal = activeTerminals[0];
        }

        var reserved = await _pairingStore.ReserveNextTransactionSequenceAsync(device.Id, cancellationToken);
        var saleReference = string.IsNullOrWhiteSpace(request.SaleReference)
            ? GenerateSaleReference(device.Id, selectedTerminal.SourceReference, DateTime.Now)
            : request.SaleReference.Trim();

        var pavoRequest = new PavoStartPaymentRequest
        {
            PosCihaziId = 0,
            PosOdemeIslemiId = 0,
            PosTerminalId = 0,
            SaleReference = saleReference,
            Amount = request.Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "TRY" : request.CurrencyCode.Trim(),
            Description = request.Description?.Trim(),
            InstallmentCount = request.InstallmentCount,
            MinInstallmentCount = request.MinInstallmentCount,
            MaxInstallmentCount = request.MaxInstallmentCount,
            IsPfInstallmentEnabled = request.IsPfInstallmentEnabled,
            Puan = request.Puan,
            SelectedSlots = request.SelectedSlots,
            CardReadTimeout = request.CardReadTimeout,
            AllowDismissCardRead = request.AllowDismissCardRead,
            PinEntryTimeout = request.PinEntryTimeout,
            SelectedTerminals = [selectedTerminal.TerminalId],
            CustomApp = request.CustomApp,
            CustomLogin = request.CustomLogin,
            CustomCommission = request.CustomCommission,
            PrintReceipt = request.PrintReceipt,
            ResponseBeforePrintEnabled = request.ResponseBeforePrintEnabled,
            CustomerReceiptPrintEnabled = request.CustomerReceiptPrintEnabled,
            MerchantReceiptPrintEnabled = request.MerchantReceiptPrintEnabled,
            ReceiptImage = request.ReceiptImage,
            CustomerReceiptImageEnabled = request.CustomerReceiptImageEnabled,
            MerchantReceiptImageEnabled = request.MerchantReceiptImageEnabled,
            ReceiptWidth = request.ReceiptWidth,
            HeadUnmaskLength = request.HeadUnmaskLength,
            TailUnmaskLength = request.TailUnmaskLength,
            ReceiptHeader = request.ReceiptHeader,
            ReceiptFooter = request.ReceiptFooter,
            ReceiptJsonEnabled = request.ReceiptJsonEnabled,
            CustomerReceiptJsonEnabled = request.CustomerReceiptJsonEnabled,
            MerchantReceiptJsonEnabled = request.MerchantReceiptJsonEnabled,
            ReceiptTextEnabled = request.ReceiptTextEnabled,
            ReceiptTextWidth = request.ReceiptTextWidth,
            CustomerReceiptTextEnabled = request.CustomerReceiptTextEnabled,
            CustomerReceiptTextWidth = request.CustomerReceiptTextWidth,
            MerchantReceiptTextEnabled = request.MerchantReceiptTextEnabled,
            MerchantReceiptTextWidth = request.MerchantReceiptTextWidth,
            TransactionHandle = new PavoTransactionHandle
            {
                SerialNumber = device.SerialNumber ?? string.Empty,
                Fingerprint = pairingState.Fingerprint,
                TransactionSequence = reserved.TransactionSequence,
                TransactionDate = DateTime.Now
            }
        };

        var response = await _pavoRestClient.StartPaymentAsync(pavoRequest, cancellationToken);
        await MarkSuccessfulInteractionAsync(device, cancellationToken);
        return new LocalDevicePaymentTestResult
        {
            DeviceId = device.Id,
            Success = !response.HasError && !response.HasAbondon && response.Data?.IsSuccessful == true,
            Status = response.Data?.TransactionStatus ?? (response.HasError ? "Failed" : response.HasAbondon ? "Abondoned" : "Unknown"),
            Message = response.Message ?? response.Data?.Message ?? "Ödeme testi tamamlandı.",
            ErrorCode = response.ErrorCode ?? response.Data?.ResultCode,
            SaleReference = saleReference,
            Amount = request.Amount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "TRY" : request.CurrencyCode.Trim(),
            SelectedTerminalId = selectedTerminal.TerminalId,
            Data = response.Data,
            Response = response,
            TestedAt = DateTimeOffset.UtcNow
        };
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

    public async Task<AgentPavoDeviceRegistrationResult> RegisterAsync(PavoDeviceProvisioningCandidate candidate, AgentSelfDto agentSelf, CancellationToken cancellationToken)
    {
        if (candidate is null)
        {
            throw new InvalidOperationException("Provisioning candidate zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(candidate.LocalDeviceId))
        {
            throw new InvalidOperationException("Local cihaz seçimi zorunludur.");
        }

        var device = await GetDeviceOrThrowAsync(candidate.LocalDeviceId, cancellationToken);
        ValidatePavoLocalDevice(device, "Provisioning");
        ValidateTesisSelection(agentSelf, candidate.TesisId ?? 0);

        if (!string.Equals(candidate.Provider, "PAVO", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sadece PAVO provider destekleniyor.");
        }

        if (!string.Equals(device.DisplayName, candidate.DisplayName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Provisioning candidate yerel cihaz ile eşleşmiyor.");
        }

        if (!string.Equals(device.Host, candidate.Host, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Provisioning candidate host değeri yerel cihaz ile eşleşmiyor.");
        }

        if (device.Protocol == LocalDeviceProtocol.Http && !string.Equals(candidate.Protocol, "HTTP", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Provisioning candidate protocol değeri yerel cihaz ile eşleşmiyor.");
        }

        if (device.Protocol == LocalDeviceProtocol.Https && !string.Equals(candidate.Protocol, "HTTPS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Provisioning candidate protocol değeri yerel cihaz ile eşleşmiyor.");
        }

        if (candidate.HttpPort != device.HttpPort || candidate.HttpsPort != device.HttpsPort)
        {
            throw new InvalidOperationException("Provisioning candidate port değerleri yerel cihaz ile eşleşmiyor.");
        }

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (pairingState is null || pairingState.PairingStatus != LocalDevicePairingStatus.Paired)
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }

        var internalRequest = new AgentPavoDeviceRegisterRequest
        {
            LocalDeviceId = device.Id,
            Provider = candidate.Provider,
            DisplayName = candidate.DisplayName,
            Host = candidate.Host,
            HttpPort = candidate.HttpPort,
            HttpsPort = candidate.HttpsPort,
            Protocol = candidate.Protocol,
            SerialNumber = candidate.SerialNumber,
            DeviceName = candidate.DeviceName,
            PairedAt = candidate.PairedAt,
            TesisId = candidate.TesisId,
            Fingerprint = pairingState.Fingerprint,
            TargetFingerprint = pairingState.TargetFingerprint,
            TransactionSequence = pairingState.TransactionSequence,
            Terminals = candidate.Terminals
        };

        try
        {
            var agentApiClient = _agentApiClient ?? throw new InvalidOperationException("Agent API client tanımlı değil.");
            var result = await agentApiClient.RegisterPavoDeviceAsync(internalRequest, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var updatedDevice = CloneForPublicUpdate(device);
            updatedDevice.CentralPosCihaziId = result.CentralPosCihaziId;
            updatedDevice.CentralAgentId = agentSelf.AgentId;
            updatedDevice.CentralTesisId = candidate.TesisId;
            updatedDevice.LastProvisionedAt = result.LastProvisionedAt == default ? now : result.LastProvisionedAt;
            updatedDevice.ProvisioningStatus = ParseProvisioningStatus(result.ProvisioningStatus);
            updatedDevice.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.InSync;
            updatedDevice.StysReconciliationMessage = "STYS kaydı senkron.";
            updatedDevice.StysReconciliationCheckedAt = now;
            updatedDevice.UpdatedAt = now;
            await _store.UpdateAsync(updatedDevice, cancellationToken);

            return result;
        }
        catch (AgentApiException ex)
        {
            var failed = CloneForPublicUpdate(device);
            failed.ProvisioningStatus = MapProvisioningFailure(ex);
            failed.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.OwnershipConflict;
            failed.StysReconciliationMessage = ex.Message;
            failed.StysReconciliationCheckedAt = DateTimeOffset.UtcNow;
            failed.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpdateAsync(failed, cancellationToken);
            throw;
        }
        catch
        {
            var failed = CloneForPublicUpdate(device);
            failed.ProvisioningStatus = LocalDeviceProvisioningStatus.Failed;
            failed.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.CentralMissing;
            failed.StysReconciliationMessage = "STYS kaydı tamamlanamadı.";
            failed.StysReconciliationCheckedAt = DateTimeOffset.UtcNow;
            failed.UpdatedAt = DateTimeOffset.UtcNow;
            await _store.UpdateAsync(failed, cancellationToken);
            throw;
        }
    }

    public async Task<LocalDeviceStysReconciliationResult> CheckStysStatusAsync(string id, CancellationToken cancellationToken)
    {
        var device = await GetDeviceOrThrowAsync(id, cancellationToken);
        ValidatePavoLocalDevice(device, "STYS durumu kontrolü");

        if (string.IsNullOrWhiteSpace(device.SerialNumber))
        {
            var now = DateTimeOffset.UtcNow;
            var noSerialDevice = CloneForPublicUpdate(device);
            noSerialDevice.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.CentralMissing;
            noSerialDevice.StysReconciliationMessage = "Önce cihaz bilgisi alınmalıdır.";
            noSerialDevice.StysReconciliationCheckedAt = now;
            noSerialDevice.UpdatedAt = now;
            await _store.UpdateAsync(noSerialDevice, cancellationToken);

            return new LocalDeviceStysReconciliationResult
            {
                DeviceId = device.Id,
                Status = LocalDeviceStysReconciliationStatus.CentralMissing,
                Message = "Önce cihaz bilgisi alınmalıdır.",
                CheckedAt = now
            };
        }

        var snapshot = await GetCentralSnapshotAsync(device, cancellationToken);
        var checkedAt = DateTimeOffset.UtcNow;
        var hasReProvisionDifferences = snapshot is not null && await HasReProvisionDifferencesAsync(device, snapshot, cancellationToken);
        var status = ResolveReconciliationStatus(device, snapshot, hasReProvisionDifferences);
        var message = ResolveReconciliationMessage(status);

        var updatedDevice = CloneForPublicUpdate(device);
        updatedDevice.ProvisioningStatus = status switch
        {
            LocalDeviceStysReconciliationStatus.InSync => LocalDeviceProvisioningStatus.Provisioned,
            LocalDeviceStysReconciliationStatus.ReProvisionRequired => LocalDeviceProvisioningStatus.ReProvisionRequired,
            LocalDeviceStysReconciliationStatus.Disabled => LocalDeviceProvisioningStatus.Disabled,
            LocalDeviceStysReconciliationStatus.OwnershipConflict => LocalDeviceProvisioningStatus.Conflict,
            LocalDeviceStysReconciliationStatus.CentralMissing => device.CentralPosCihaziId.HasValue
                ? LocalDeviceProvisioningStatus.Conflict
                : LocalDeviceProvisioningStatus.NotProvisioned,
            _ => device.ProvisioningStatus
        };

        if (snapshot is not null)
        {
            updatedDevice.CentralPosCihaziId = snapshot.CentralPosCihaziId;
            updatedDevice.CentralAgentId = snapshot.AgentId;
            updatedDevice.CentralTesisId = snapshot.TesisId;
        }

        updatedDevice.StysReconciliationStatus = status;
        updatedDevice.StysReconciliationMessage = message;
        updatedDevice.StysReconciliationCheckedAt = checkedAt;
        updatedDevice.UpdatedAt = checkedAt;
        await _store.UpdateAsync(updatedDevice, cancellationToken);

        return new LocalDeviceStysReconciliationResult
        {
            DeviceId = device.Id,
            Status = status,
            Message = message,
            CentralPosCihaziId = snapshot?.CentralPosCihaziId,
            CentralAgentId = snapshot?.AgentId,
            CentralTesisId = snapshot?.TesisId,
            CentralSerialNumber = snapshot?.SerialNumber,
            CentralProvider = snapshot?.Provider,
            CentralActive = snapshot?.Active,
            CheckedAt = checkedAt
        };
    }

    private async Task<DeviceInfoExecution> ExecuteDeviceInfoAsync(LocalDevice device, bool requirePairing, CancellationToken cancellationToken)
    {
        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (requirePairing)
        {
            EnsurePaired(pairingState);
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
        await MarkSuccessfulInteractionAsync(updatedDevice, cancellationToken);

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
            await MarkSuccessfulInteractionAsync(device, cancellationToken);
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
            if (device.CentralPosCihaziId.HasValue || device.ProvisioningStatus == LocalDeviceProvisioningStatus.Provisioned)
            {
                updatedDevice.ProvisioningStatus = LocalDeviceProvisioningStatus.ReProvisionRequired;
                updatedDevice.StysReconciliationStatus = LocalDeviceStysReconciliationStatus.ReProvisionRequired;
                updatedDevice.StysReconciliationMessage = "Pairing yenilendi; yeniden STYS eşitleme gerekli.";
                updatedDevice.StysReconciliationCheckedAt = now;
            }
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

    private async Task MarkSuccessfulInteractionAsync(LocalDevice device, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        device.Status = LocalDeviceConnectionStatus.Connected;
        device.LastConnectionSuccess = true;
        device.LastConnectionTestAt = now;
        device.LastError = null;
        device.UpdatedAt = now;
        await _store.UpdateAsync(CloneForPublicUpdate(device), cancellationToken);
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
        var centralPosCihaziId = existing?.CentralPosCihaziId;
        var centralAgentId = existing?.CentralAgentId;
        var centralTesisId = existing?.CentralTesisId;
        var reconciliationStatus = existing?.StysReconciliationStatus;
        var reconciliationMessage = existing?.StysReconciliationMessage;
        var reconciliationCheckedAt = existing?.StysReconciliationCheckedAt;

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
            CentralPosCihaziId = centralPosCihaziId,
            CentralAgentId = centralAgentId,
            CentralTesisId = centralTesisId,
            StysReconciliationStatus = reconciliationStatus,
            StysReconciliationMessage = reconciliationMessage,
            StysReconciliationCheckedAt = reconciliationCheckedAt,
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
            TransactionDate = DateTime.Now
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
            TransactionDate = DateTime.Now
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
        CentralPosCihaziId = device.CentralPosCihaziId,
        CentralAgentId = device.CentralAgentId,
        CentralTesisId = device.CentralTesisId,
        LastProvisionedAt = device.LastProvisionedAt,
        ProvisioningStatus = device.ProvisioningStatus,
        StysReconciliationStatus = device.StysReconciliationStatus,
        StysReconciliationMessage = device.StysReconciliationMessage,
        StysReconciliationCheckedAt = device.StysReconciliationCheckedAt,
        CreatedAt = device.CreatedAt,
        UpdatedAt = device.UpdatedAt
    };

    private async Task<AgentPavoDeviceStatusSnapshotDto?> GetCentralSnapshotAsync(LocalDevice device, CancellationToken cancellationToken)
    {
        if (_agentApiClient is null)
        {
            throw new InvalidOperationException("Agent API client tanımlı değil.");
        }

        return await _agentApiClient.GetPavoDeviceStatusSnapshotAsync(new AgentPavoDeviceStatusSnapshotRequest
        {
            LocalDeviceId = device.Id,
            Provider = "PAVO",
            SerialNumber = device.SerialNumber
        }, cancellationToken);
    }

    private static LocalDeviceStysReconciliationStatus ResolveReconciliationStatus(LocalDevice device, AgentPavoDeviceStatusSnapshotDto? snapshot, bool hasReProvisionDifferences)
    {
        if (snapshot is null)
        {
            return LocalDeviceStysReconciliationStatus.CentralMissing;
        }

        if (!snapshot.Active)
        {
            return LocalDeviceStysReconciliationStatus.Disabled;
        }

        if (!string.Equals(snapshot.Provider, "PAVO", StringComparison.OrdinalIgnoreCase)
            || !Nullable.Equals(device.CentralPosCihaziId, snapshot.CentralPosCihaziId)
            || !Nullable.Equals(device.CentralAgentId, snapshot.AgentId)
            || !Nullable.Equals(device.CentralTesisId, snapshot.TesisId)
            || (!string.IsNullOrWhiteSpace(snapshot.AgentLocalDeviceId) && !string.Equals(snapshot.AgentLocalDeviceId, device.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return LocalDeviceStysReconciliationStatus.OwnershipConflict;
        }

        if (hasReProvisionDifferences)
        {
            return LocalDeviceStysReconciliationStatus.ReProvisionRequired;
        }

        return LocalDeviceStysReconciliationStatus.InSync;
    }

    private static string ResolveReconciliationMessage(LocalDeviceStysReconciliationStatus status) => status switch
    {
        LocalDeviceStysReconciliationStatus.InSync => "STYS kaydı senkron.",
        LocalDeviceStysReconciliationStatus.ReProvisionRequired => "Cihaz değişiklikleri algılandı; yeniden STYS eşitleme gerekli.",
        LocalDeviceStysReconciliationStatus.CentralMissing => "Merkezi STYS kaydı bulunamadı.",
        LocalDeviceStysReconciliationStatus.OwnershipConflict => "Bu cihaz başka Agent'a veya tesise bağlı.",
        LocalDeviceStysReconciliationStatus.Disabled => "Merkezi STYS kaydı devre dışı.",
        _ => "STYS durumu kontrol edilemedi."
    };

    private async Task<bool> HasReProvisionDifferencesAsync(LocalDevice device, AgentPavoDeviceStatusSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        if (!string.Equals(NormalizeText(snapshot.Host), NormalizeText(device.Host), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((snapshot.HttpPort ?? 0) != device.HttpPort || (snapshot.HttpsPort ?? 0) != device.HttpsPort)
        {
            return true;
        }

        if (!string.Equals(NormalizeText(snapshot.DisplayName), NormalizeText(device.DisplayName), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(NormalizeText(snapshot.SerialNumber), NormalizeText(device.SerialNumber), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var centralTerminals = (snapshot.Terminals ?? [])
            .Where(x => x.Active)
            .Select(BuildTerminalIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var localTerminals = (await _terminalStore.GetByLocalDeviceIdAsync(device.Id, cancellationToken))
            .Where(x => x.Active)
            .Select(BuildTerminalIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return !centralTerminals.SetEquals(localTerminals);
    }

    private static string BuildTerminalIdentity(LocalDeviceTerminal terminal) =>
        $"{terminal.AcquirerId?.Trim().ToUpperInvariant() ?? string.Empty}:{terminal.TerminalId.Trim()}";

    private static string BuildTerminalIdentity(PavoDeviceProvisioningCandidateTerminal terminal) =>
        $"{terminal.AcquirerId?.Trim().ToUpperInvariant() ?? string.Empty}:{terminal.TerminalId.Trim()}";

    private static string GenerateSaleReference(string deviceId, string terminalReference, DateTime now)
    {
        var safeTerminal = string.IsNullOrWhiteSpace(terminalReference) ? "TERM" : terminalReference.Trim();
        var value = $"STYS-PAY-{now:yyyyMMddHHmmssfff}-{deviceId}-{safeTerminal}-{Guid.NewGuid():N}";
        return value.Length <= 96 ? value : value[..96];
    }

    private static bool HasLifecycleRelevantChanges(LocalDevice existing, LocalDevice updated)
    {
        return !string.Equals(existing.DisplayName, updated.DisplayName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.Host, updated.Host, StringComparison.OrdinalIgnoreCase)
            || existing.HttpPort != updated.HttpPort
            || existing.HttpsPort != updated.HttpsPort
            || existing.DeviceType != updated.DeviceType
            || existing.Provider != updated.Provider
            || !string.Equals(existing.SerialNumber, updated.SerialNumber, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.DeviceName, updated.DeviceName, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalDeviceProvisioningStatus MapProvisioningFailure(AgentApiException ex) =>
        ex.StatusCode switch
        {
            System.Net.HttpStatusCode.Conflict => LocalDeviceProvisioningStatus.Conflict,
            System.Net.HttpStatusCode.Forbidden => LocalDeviceProvisioningStatus.Disabled,
            _ => LocalDeviceProvisioningStatus.Failed
        };

    private static LocalDeviceProvisioningStatus ParseProvisioningStatus(string? status) =>
        Enum.TryParse<LocalDeviceProvisioningStatus>(status, true, out var parsed)
            ? parsed
            : LocalDeviceProvisioningStatus.NotProvisioned;

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
