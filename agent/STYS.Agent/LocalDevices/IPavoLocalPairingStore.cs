namespace STYS.Agent.LocalDevices;

public interface IPavoLocalPairingStore
{
    Task<PavoLocalPairingState?> GetAsync(string deviceId, CancellationToken cancellationToken);
    Task<PavoLocalPairingState> UpsertAsync(PavoLocalPairingState state, CancellationToken cancellationToken);
    Task<PavoLocalPairingState> ReserveNextTransactionSequenceAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Sequence reservation specifically for a Pairing request. Reference protocol semantics:
    /// while the device has never been successfully paired, every Pairing attempt (including retries)
    /// uses TransactionSequence == 1, regardless of any pre-pair diagnostic calls (GetDeviceInfo, etc.)
    /// that may have already advanced the stored counter. Once PairingStatus == Paired, this behaves
    /// like a normal monotonic reservation (used by force re-pair) and never resets back to 1.</summary>
    Task<PavoLocalPairingState> ReservePairingSequenceAsync(string deviceId, CancellationToken cancellationToken);

    Task DeleteAsync(string deviceId, CancellationToken cancellationToken);
}
