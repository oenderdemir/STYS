using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;

namespace STYS.Agent.Workers;

public sealed class PavoCommandSequenceReservationService : IPavoCommandSequenceReservationService
{
    private readonly ILocalDeviceStore _localDeviceStore;
    private readonly IPavoLocalPairingStore _pairingStore;
    private readonly string _pavoFingerprint;

    public PavoCommandSequenceReservationService(
        ILocalDeviceStore localDeviceStore,
        IPavoLocalPairingStore pairingStore,
        IOptions<PavoAgentOptions>? pavoOptions = null)
    {
        _localDeviceStore = localDeviceStore;
        _pairingStore = pairingStore;
        _pavoFingerprint = PavoAgentOptions.ResolveFingerprint(
            pavoOptions?.Value.Fingerprint,
            Environment.GetEnvironmentVariable(PavoAgentOptions.FingerprintEnvironmentVariable));
    }

    public async Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, string? serialNumber, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        var device = await GetPavoDeviceAsync(centralPosCihaziId, serialNumber, cancellationToken);

        if (device.ProvisioningStatus is LocalDeviceProvisioningStatus.ReProvisionRequired
            or LocalDeviceProvisioningStatus.Conflict
            or LocalDeviceProvisioningStatus.Disabled)
        {
            throw new InvalidOperationException("Bu cihazın STYS durumu komut çalıştırmaya uygun değil.");
        }

        if (device.ProvisioningStatus != LocalDeviceProvisioningStatus.Provisioned || device.CentralPosCihaziId != centralPosCihaziId)
        {
            throw new InvalidOperationException("Bu cihaz henüz STYS'e kaydedilmemiş.");
        }

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken);
        if (pairingState is null || pairingState.PairingStatus != LocalDevicePairingStatus.Paired)
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }

        return await ReserveHandleAsync(device, transactionDate, cancellationToken);
    }

    public async Task<PavoTransactionHandle> ReserveForPairingAsync(int centralPosCihaziId, string? serialNumber, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        var device = await GetPavoDeviceAsync(centralPosCihaziId, serialNumber, cancellationToken);
        return await ReserveHandleAsync(device, transactionDate, cancellationToken);
    }

    public async Task AdvanceAsync(int centralPosCihaziId, string? serialNumber, CancellationToken cancellationToken)
    {
        var device = await GetPavoDeviceAsync(centralPosCihaziId, serialNumber, cancellationToken);
        await _pairingStore.AdvanceOutgoingSequenceAsync(device.Id, cancellationToken);
    }

    private async Task<LocalDevice> GetPavoDeviceAsync(int centralPosCihaziId, string? serialNumber, CancellationToken cancellationToken)
    {
        var devices = await _localDeviceStore.GetAllAsync(cancellationToken);
        var device = devices.FirstOrDefault(x => x.CentralPosCihaziId == centralPosCihaziId)
            ?? ResolveBySerialNumber(devices, serialNumber)
            ?? throw new InvalidOperationException("Bu POS cihazına bağlı yerel PAVO cihazı bulunamadı.");

        if (device.Provider is not LocalDeviceProvider.Pavo || device.DeviceType is not LocalDeviceType.Pos)
        {
            throw new InvalidOperationException("Sadece PAVO POS cihazları destekleniyor.");
        }

        if (string.IsNullOrWhiteSpace(device.SerialNumber))
        {
            throw new InvalidOperationException("PAVO pairing için seri numarası zorunludur.");
        }

        return device;
    }

    private static LocalDevice? ResolveBySerialNumber(IEnumerable<LocalDevice> devices, string? serialNumber)
    {
        var normalizedSerial = serialNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSerial))
        {
            return null;
        }

        return devices.FirstOrDefault(x =>
            x.Provider is LocalDeviceProvider.Pavo
            && x.DeviceType is LocalDeviceType.Pos
            && string.Equals(x.SerialNumber, normalizedSerial, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PavoTransactionHandle> ReserveHandleAsync(LocalDevice device, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        // Peek only. Reference semantics advance the outgoing sequence once the device has actually
        // answered; callers advance via AdvanceAsync as soon as the HTTP response is received.
        var sequence = await _pairingStore.PeekOutgoingSequenceAsync(device.Id, cancellationToken);
        return new PavoTransactionHandle
        {
            SerialNumber = device.SerialNumber ?? string.Empty,
            Fingerprint = _pavoFingerprint,
            TransactionSequence = sequence,
            TransactionDate = transactionDate ?? DateTime.Now
        };
    }
}
