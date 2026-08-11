namespace STYS.Agent.LocalDevices;

public interface IPavoLocalPairingStore
{
    Task<PavoLocalPairingState?> GetAsync(string deviceId, CancellationToken cancellationToken);
    Task<PavoLocalPairingState> UpsertAsync(PavoLocalPairingState state, CancellationToken cancellationToken);
    Task<PavoLocalPairingState> ReserveNextTransactionSequenceAsync(string deviceId, CancellationToken cancellationToken);
    Task DeleteAsync(string deviceId, CancellationToken cancellationToken);
}
