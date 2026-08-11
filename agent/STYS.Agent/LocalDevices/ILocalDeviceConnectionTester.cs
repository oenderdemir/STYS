namespace STYS.Agent.LocalDevices;

public interface ILocalDeviceConnectionTester
{
    LocalDeviceProvider Provider { get; }
    Task<LocalDeviceConnectionTestResult> TestAsync(LocalDevice device, CancellationToken cancellationToken);
}

public interface ILocalDeviceConnectionTesterRegistry
{
    bool TryGetTester(LocalDeviceProvider provider, out ILocalDeviceConnectionTester tester);
}
