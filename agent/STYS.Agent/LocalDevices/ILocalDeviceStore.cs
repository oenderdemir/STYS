namespace STYS.Agent.LocalDevices;

public interface ILocalDeviceStore
{
    Task<IReadOnlyCollection<LocalDevice>> GetAllAsync(CancellationToken cancellationToken);
    Task<LocalDevice?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<LocalDevice> CreateAsync(LocalDevice device, CancellationToken cancellationToken);
    Task<LocalDevice> UpdateAsync(LocalDevice device, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
