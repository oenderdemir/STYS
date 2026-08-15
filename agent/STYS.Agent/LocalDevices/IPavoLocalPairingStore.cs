namespace STYS.Agent.LocalDevices;

public interface IPavoLocalPairingStore
{
    Task<PavoLocalPairingState?> GetAsync(string deviceId, CancellationToken cancellationToken);
    Task<PavoLocalPairingState> UpsertAsync(PavoLocalPairingState state, CancellationToken cancellationToken);

    /// <summary>Returns the sequence number the next outgoing PAVO request must carry, WITHOUT
    /// consuming it. Mirrors the reference client, which reads its current counter to build the
    /// request and only advances once the device has actually answered. Never returns less than 1
    /// (reference InitialTransactionSequence).</summary>
    Task<long> PeekOutgoingSequenceAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>Advances the outgoing sequence by one. Call this after an HTTP response was
    /// received from the device - regardless of HTTP status, business error, or an unparseable
    /// body - and NOT after a connection error or timeout. This is exactly the reference client's
    /// `_transactionSequence++` placement.</summary>
    Task<PavoLocalPairingState> AdvanceOutgoingSequenceAsync(string deviceId, CancellationToken cancellationToken);

    Task DeleteAsync(string deviceId, CancellationToken cancellationToken);
}
