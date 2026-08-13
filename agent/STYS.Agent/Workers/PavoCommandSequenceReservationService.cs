using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;

namespace STYS.Agent.Workers;

public interface IPavoCommandSequenceReservationService
{
    Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken);
}

public sealed class PavoCommandSequenceReservationService : IPavoCommandSequenceReservationService
{
    private readonly ILocalDeviceStore _localDeviceStore;
    private readonly IPavoLocalPairingStore _pairingStore;

    public PavoCommandSequenceReservationService(
        ILocalDeviceStore localDeviceStore,
        IPavoLocalPairingStore pairingStore)
    {
        _localDeviceStore = localDeviceStore;
        _pairingStore = pairingStore;
    }

    public async Task<PavoTransactionHandle> ReserveAsync(int centralPosCihaziId, DateTime? transactionDate, CancellationToken cancellationToken)
    {
        var device = await _localDeviceStore.GetByCentralPosCihaziIdAsync(centralPosCihaziId, cancellationToken)
            ?? throw new InvalidOperationException("Bu POS cihazına bağlı yerel PAVO cihazı bulunamadı.");

        if (device.Provider is not LocalDeviceProvider.Pavo || device.DeviceType is not LocalDeviceType.Pos)
        {
            throw new InvalidOperationException("Sadece PAVO POS cihazları destekleniyor.");
        }

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

        var pairingState = await _pairingStore.GetAsync(device.Id, cancellationToken)
            ?? throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");

        if (string.IsNullOrWhiteSpace(pairingState.Fingerprint))
        {
            throw new InvalidOperationException("Önce PAVO cihazı ile pairing yapılmalıdır.");
        }

        var reserved = await _pairingStore.ReserveNextTransactionSequenceAsync(device.Id, cancellationToken);
        return new PavoTransactionHandle
        {
            SerialNumber = device.SerialNumber ?? string.Empty,
            Fingerprint = pairingState.Fingerprint,
            TransactionSequence = reserved.TransactionSequence,
            TransactionDate = transactionDate ?? DateTime.Now
        };
    }
}
