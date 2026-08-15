using STYS.Agent.Contracts.Dtos;

namespace STYS.Agent.Modules.Pavo;

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
