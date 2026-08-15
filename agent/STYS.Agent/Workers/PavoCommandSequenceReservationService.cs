using Microsoft.Extensions.Options;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;

namespace STYS.Agent.Workers;

public interface IPavoCommandSequenceReservationService
{
    /// <summary>Sequence preparation for post-pair commands (Ping/GetDeviceInfo/StartPayment/GetPaymentResult):
    /// requires the device to already be centrally provisioned and paired.</summary>
    Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken);

    /// <summary>Sequence preparation for the central PavoPairing (re-pair) command. Deliberately does
    /// NOT require "already paired"/"already provisioned" - requiring that would make the central
    /// pairing command unable to ever perform an initial pairing.</summary>
    Task<PavoTransactionHandle> ReserveForPairingAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken);

    /// <summary>Advances the device's outgoing sequence. Call after a PAVO command whose request
    /// actually reached the device and produced an HTTP response.</summary>
    Task AdvanceAsync(int centralPosCihaziId, CancellationToken cancellationToken);
}

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

    public async Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        var device = await GetPavoDeviceAsync(centralPosCihaziId, cancellationToken);

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

    public async Task<PavoTransactionHandle> ReserveForPairingAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        var device = await GetPavoDeviceAsync(centralPosCihaziId, cancellationToken);
        return await ReserveHandleAsync(device, transactionDate, cancellationToken);
    }

    private async Task<LocalDevice> GetPavoDeviceAsync(int centralPosCihaziId, CancellationToken cancellationToken)
    {
        var device = await _localDeviceStore.GetByCentralPosCihaziIdAsync(centralPosCihaziId, cancellationToken)
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

    public async Task AdvanceAsync(int centralPosCihaziId, CancellationToken cancellationToken)
    {
        var device = await _localDeviceStore.GetByCentralPosCihaziIdAsync(centralPosCihaziId, cancellationToken);
        if (device is null)
        {
            return;
        }

        await _pairingStore.AdvanceOutgoingSequenceAsync(device.Id, cancellationToken);
    }

    private async Task<PavoTransactionHandle> ReserveHandleAsync(LocalDevice device, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        // Peek only. Reference semantics advance the outgoing sequence once the device has actually
        // answered, so the caller advances via AdvanceAsync after the command completes.
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
