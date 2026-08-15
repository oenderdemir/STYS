using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Services;

namespace STYS.Tests.Agent;

public sealed class AgentLocalDevicesPhaseA4Tests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-a4-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalDevicesPhaseA4Tests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
        }
    }

    [Fact]
    public async Task DeviceSaveLoad_PreservesValues()
    {
        var store = CreateStore();
        var device = await store.CreateAsync(new LocalDevice
        {
            DisplayName = "Kasiyer POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "192.168.1.10",
            HttpPort = 4567,
            HttpsPort = 4568,
            Protocol = LocalDeviceProtocol.Http,
            SerialNumber = "SN-001"
        }, CancellationToken.None);

        var loaded = await store.GetByIdAsync(device.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(device.Id, loaded!.Id);
        Assert.Equal("Kasiyer POS", loaded.DisplayName);
        Assert.Equal(LocalDeviceType.Pos, loaded.DeviceType);
        Assert.Equal(LocalDeviceProvider.Pavo, loaded.Provider);
        Assert.Equal("192.168.1.10", loaded.Host);
        Assert.Equal(4567, loaded.HttpPort);
        Assert.Equal(4568, loaded.HttpsPort);
        Assert.Equal(LocalDeviceProtocol.Http, loaded.Protocol);
        Assert.Equal("SN-001", loaded.SerialNumber);
    }

    [Fact]
    public async Task RestartSonrasiPersistence_Korunur()
    {
        var store1 = CreateStore();
        var saved = await store1.CreateAsync(new LocalDevice
        {
            DisplayName = "Pavo POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.5",
            Protocol = LocalDeviceProtocol.Https
        }, CancellationToken.None);

        var store2 = CreateStore();
        var loaded = await store2.GetByIdAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Pavo POS", loaded!.DisplayName);
        Assert.Equal("10.0.0.5", loaded.Host);
    }

    [Fact]
    public async Task DuplicateId_Engellenir()
    {
        var store = CreateStore();
        var device = new LocalDevice
        {
            Id = "dup-id",
            DisplayName = "Cihaz 1",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.10",
            Protocol = LocalDeviceProtocol.Http
        };

        await store.CreateAsync(device, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(new LocalDevice
            {
                Id = "dup-id",
                DisplayName = "Cihaz 2",
                DeviceType = LocalDeviceType.Pos,
                Provider = LocalDeviceProvider.Pavo,
                Host = "10.0.0.11",
                Protocol = LocalDeviceProtocol.Http
            }, CancellationToken.None));
    }

    [Theory]
    [InlineData("file://evil")]
    [InlineData("https://evil")]
    [InlineData("192.168.1.10/path")]
    public async Task InvalidHost_Reddedilir(string host)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(new LocalDeviceUpsertRequest
            {
                DisplayName = "Cihaz",
                DeviceType = LocalDeviceType.Pos,
                Provider = LocalDeviceProvider.Pavo,
                Host = host,
                Protocol = LocalDeviceProtocol.Http
            }, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidProtocol_Reddedilir()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(new LocalDeviceUpsertRequest
            {
                DisplayName = "Cihaz",
                DeviceType = LocalDeviceType.Pos,
                Provider = LocalDeviceProvider.Pavo,
                Host = "10.0.0.10",
                Protocol = (LocalDeviceProtocol)99
            }, CancellationToken.None));
    }

    [Fact]
    public async Task PavoDefaultPorts_Uygulanir()
    {
        var service = CreateService();
        var saved = await service.SaveAsync(new LocalDeviceUpsertRequest
        {
            DisplayName = "Pavo POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.11",
            Protocol = LocalDeviceProtocol.Http,
            HttpPort = null,
            HttpsPort = null
        }, CancellationToken.None);

        Assert.Equal(4567, saved.HttpPort);
        Assert.Equal(4568, saved.HttpsPort);
    }

    [Fact]
    public async Task ConnectionSuccess_StatusUpdated_AndErrorCleared()
    {
        var store = CreateStore();
        var saved = await store.CreateAsync(new LocalDevice
        {
            DisplayName = "Pavo POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.12",
            Protocol = LocalDeviceProtocol.Http
        }, CancellationToken.None);

        var service = CreateService(new[]
        {
            new FixedTester(LocalDeviceProvider.Pavo, new LocalDeviceConnectionTestResult
            {
                Status = LocalDeviceConnectionStatus.Connected,
                Success = true,
                Message = "Bağlantı başarılı.",
                TestedAt = DateTimeOffset.UtcNow
            })
        }, store);

        var result = await service.TestAsync(saved.Id, CancellationToken.None);
        var loaded = await store.GetByIdAsync(saved.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, result.Status);
        Assert.NotNull(loaded);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, loaded!.Status);
        Assert.True(loaded.LastConnectionSuccess);
        Assert.Null(loaded.LastError);
        Assert.NotNull(loaded.LastConnectionTestAt);
    }

    [Fact]
    public async Task ConnectionTimeout_StatusUpdated()
    {
        var store = CreateStore();
        var saved = await store.CreateAsync(new LocalDevice
        {
            DisplayName = "Pavo POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.13",
            Protocol = LocalDeviceProtocol.Http
        }, CancellationToken.None);

        var service = CreateService(new[]
        {
            new FixedTester(LocalDeviceProvider.Pavo, new LocalDeviceConnectionTestResult
            {
                Status = LocalDeviceConnectionStatus.Timeout,
                Success = false,
                Message = "Zaman aşımı.",
                TestedAt = DateTimeOffset.UtcNow
            })
        }, store);

        var result = await service.TestAsync(saved.Id, CancellationToken.None);
        var loaded = await store.GetByIdAsync(saved.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, result.Status);
        Assert.NotNull(loaded);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, loaded!.Status);
        Assert.True(loaded.LastConnectionSuccess);
        Assert.Null(loaded.LastError);
        Assert.NotNull(loaded.LastConnectionTestAt);
    }

    [Fact]
    public async Task ConnectionUnreachable_StatusUpdated()
    {
        var store = CreateStore();
        var saved = await store.CreateAsync(new LocalDevice
        {
            DisplayName = "Pavo POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.14",
            Protocol = LocalDeviceProtocol.Http
        }, CancellationToken.None);

        var service = CreateService(new[]
        {
            new FixedTester(LocalDeviceProvider.Pavo, new LocalDeviceConnectionTestResult
            {
                Status = LocalDeviceConnectionStatus.Unreachable,
                Success = false,
                Message = "Erişilemiyor.",
                TestedAt = DateTimeOffset.UtcNow
            })
        }, store);

        var result = await service.TestAsync(saved.Id, CancellationToken.None);
        var loaded = await store.GetByIdAsync(saved.Id, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, result.Status);
        Assert.NotNull(loaded);
        Assert.Equal(LocalDeviceConnectionStatus.Connected, loaded!.Status);
        Assert.True(loaded.LastConnectionSuccess);
        Assert.Null(loaded.LastError);
        Assert.NotNull(loaded.LastConnectionTestAt);
    }

    [Fact]
    public async Task Delete_RemovesDevice()
    {
        var service = CreateService();
        var saved = await service.SaveAsync(new LocalDeviceUpsertRequest
        {
            DisplayName = "Silinecek POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.15",
            Protocol = LocalDeviceProtocol.Http
        }, CancellationToken.None);

        await service.DeleteAsync(saved.Id, CancellationToken.None);

        var loaded = await CreateStore().GetByIdAsync(saved.Id, CancellationToken.None);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task GenericModel_SecretAlanIcermez()
    {
        var store = CreateStore();
        var saved = await store.CreateAsync(new LocalDevice
        {
            DisplayName = "Gizli Alan Yok",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "10.0.0.16",
            Protocol = LocalDeviceProtocol.Http
        }, CancellationToken.None);

        var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "local-devices.json"));
        Assert.DoesNotContain("Fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(saved.Id, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocalDeviceTester_ArbitrarySchemeKabulEtmez()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(new LocalDeviceUpsertRequest
            {
                DisplayName = "Evil",
                DeviceType = LocalDeviceType.Pos,
                Provider = LocalDeviceProvider.Pavo,
                Host = "file://share",
                Protocol = LocalDeviceProtocol.Http
            }, CancellationToken.None));
    }

    [Fact]
    public void LastStysConnectionError_TrackingCalisir()
    {
        var status = new AgentRuntimeStatus();
        status.MarkFailedConnection("STYS erişilemiyor.");

        Assert.Equal("STYS erişilemiyor.", status.LastStysConnectionError);

        status.MarkSuccessfulConnection();

        Assert.Null(status.LastStysConnectionError);
    }

    [Fact]
    public async Task PavoConnectionTester_EndpointUretir()
    {
        var client = new RecordingPavoClient();
        var tester = new PavoLocalDeviceConnectionTester(client, NullLogger<PavoLocalDeviceConnectionTester>.Instance);

        var result = await tester.TestAsync(new LocalDevice
        {
            Host = "10.0.0.20",
            Protocol = LocalDeviceProtocol.Https,
            HttpsPort = 4568
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("https://10.0.0.20:4568/", client.LastEndpoint);
    }

    [Fact]
    public void ConnectionTesterRegistry_PavoIcinTesterDonderir()
    {
        var registry = new LocalDeviceConnectionTesterRegistry([new FixedTester(LocalDeviceProvider.Pavo, new LocalDeviceConnectionTestResult())]);

        Assert.True(registry.TryGetTester(LocalDeviceProvider.Pavo, out var tester));
        Assert.Equal(LocalDeviceProvider.Pavo, tester.Provider);
    }

    private FileLocalDeviceStore CreateStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceStore>.Instance);

    private LocalDeviceManagementService CreateService(
        IEnumerable<ILocalDeviceConnectionTester>? testers = null,
        FileLocalDeviceStore? store = null,
        IPavoRestClient? pavoRestClient = null,
        FilePavoLocalPairingStore? pairingStore = null)
    {
        return new LocalDeviceManagementService(
            store ?? CreateStore(),
            CreateTerminalStore(),
            new LocalDeviceConnectionTesterRegistry(testers ?? [new FixedTester(LocalDeviceProvider.Pavo, new LocalDeviceConnectionTestResult
            {
                Status = LocalDeviceConnectionStatus.Connected,
                Success = true,
                Message = "Bağlantı başarılı.",
                TestedAt = DateTimeOffset.UtcNow
            })]),
            pairingStore ?? CreatePairingStore(),
            pavoRestClient ?? new DummyPavoRestClient());
    }

    private TempAgentPathResolver CreatePathResolver() => new(_tempDir);

    private FilePavoLocalPairingStore CreatePairingStore() =>
        new(CreatePathResolver(), NullLogger<FilePavoLocalPairingStore>.Instance);

    private FileLocalDeviceTerminalStore CreateTerminalStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceTerminalStore>.Instance);

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string LogDirectory => Path.Combine(DataDirectory, "logs");
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
        public string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
    }

    private sealed class FixedTester : ILocalDeviceConnectionTester
    {
        private readonly LocalDeviceConnectionTestResult _result;

        public FixedTester(LocalDeviceProvider provider, LocalDeviceConnectionTestResult result)
        {
            Provider = provider;
            _result = result;
        }

        public LocalDeviceProvider Provider { get; }

        public Task<LocalDeviceConnectionTestResult> TestAsync(LocalDevice device, CancellationToken cancellationToken)
        {
            var cloned = new LocalDeviceConnectionTestResult
            {
                DeviceId = device.Id,
                Status = _result.Status,
                Success = _result.Success,
                Message = _result.Message,
                TestedAt = _result.TestedAt == default ? DateTimeOffset.UtcNow : _result.TestedAt
            };

            return Task.FromResult(cloned);
        }
    }

    private sealed class RecordingPavoClient : IPavoClient
    {
        public string? LastEndpoint { get; private set; }

        public Task<PavoConnectionResult> TestConnectionAsync(string endpoint, int timeoutMs, CancellationToken cancellationToken)
        {
            LastEndpoint = endpoint;
            return Task.FromResult(PavoConnectionResult.Ok(12));
        }
    }

    private sealed class DummyPavoRestClient : IPavoRestClient
    {
        public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoPerformEodResponse> PerformEodAsync(PavoPerformEodRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoRebootDeviceResponse> RebootDeviceAsync(PavoRebootDeviceRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoEnterPinModeResponse> EnterPinModeAsync(PavoEnterPinModeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoExitPinModeResponse> ExitPinModeAsync(PavoExitPinModeRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
