namespace STYS.Agent.LocalDevices;

public sealed class LocalDeviceConnectionTesterRegistry : ILocalDeviceConnectionTesterRegistry
{
    private readonly IReadOnlyDictionary<LocalDeviceProvider, ILocalDeviceConnectionTester> _testers;

    public LocalDeviceConnectionTesterRegistry(IEnumerable<ILocalDeviceConnectionTester> testers)
    {
        _testers = testers.ToDictionary(x => x.Provider);
    }

    public bool TryGetTester(LocalDeviceProvider provider, out ILocalDeviceConnectionTester tester) =>
        _testers.TryGetValue(provider, out tester!);
}
