namespace STYS.Agent.LocalDevices;

public interface ILocalDeviceTerminalStore
{
    Task<IReadOnlyCollection<LocalDeviceTerminal>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LocalDeviceTerminal>> GetByLocalDeviceIdAsync(string localDeviceId, CancellationToken cancellationToken);
    Task<LocalDeviceTerminal> UpsertAsync(LocalDeviceTerminal terminal, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LocalDeviceTerminal>> ReconcileAsync(string localDeviceId, IReadOnlyCollection<LocalDeviceTerminal> discovered, CancellationToken cancellationToken);
    Task DeleteByLocalDeviceIdAsync(string localDeviceId, CancellationToken cancellationToken);
}
