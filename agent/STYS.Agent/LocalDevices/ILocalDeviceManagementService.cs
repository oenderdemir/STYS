namespace STYS.Agent.LocalDevices;

public interface ILocalDeviceManagementService
{
    Task<IReadOnlyCollection<LocalDevice>> GetAllAsync(CancellationToken cancellationToken);
    Task<LocalDevice?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<LocalDevice> SaveAsync(LocalDeviceUpsertRequest request, CancellationToken cancellationToken);
    Task<LocalDeviceConnectionTestResult> TestAsync(LocalDeviceTestRequest request, CancellationToken cancellationToken);
    Task<LocalDeviceConnectionTestResult> TestAsync(string id, CancellationToken cancellationToken);
    Task<LocalDevice> GetDeviceInfoAsync(string id, CancellationToken cancellationToken);
    Task<LocalDevice> PairAsync(string id, bool forceRePair, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}
