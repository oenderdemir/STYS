using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;

namespace STYS.Tests.Agent;

public sealed class AgentLocalDevicesPhaseB2Tests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-b2-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalDevicesPhaseB2Tests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task UnpairedCihaz_Discovery_Reddedilir()
    {
        var client = new FakePavoRestClient();
        var store = CreateStore();
        var service = CreateService(client, store: store);
        var device = await CreateSavedDeviceAsync(service);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DiscoverTerminalsAsync(device.Id, CancellationToken.None));

        Assert.Contains("pairing", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.GetDeviceInfoCallCount);
    }

    [Fact]
    public async Task PairedCihaz_TerminalDiscovery_Success()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-100",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Acquirer 1"
                    },
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-2",
                        MerchantId = "MER-2",
                        AcquirerId = "ACQ-2",
                        AcquirerName = "Acquirer 2"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        var discovered = await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var rawJson = await File.ReadAllTextAsync(CreatePathResolver().LocalDeviceTerminalsStorePath);

        Assert.Equal(2, discovered.Count);
        Assert.Equal(1, client.GetDeviceInfoCallCount);
        Assert.Equal(1, client.LastGetDeviceInfoRequest?.TransactionHandle.TransactionSequence);
        Assert.Contains(discovered, x => x.TerminalId == "TERM-1" && x.Active);
        Assert.Contains(discovered, x => x.TerminalId == "TERM-2" && x.Active);
        Assert.DoesNotContain("Fingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateDiscovery_DuplicateTerminalUretmez_veMetadataGunceller()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-200",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "First Name"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        var first = await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        client.GetDeviceInfoResponse = BuildDeviceInfoResponse(
            serialNumber: "SN-200",
            deviceName: "PAVO Model X",
            new[]
            {
                new PavoDeviceTerminalInfo
                {
                    TerminalId = "TERM-1",
                    MerchantId = "MER-1",
                    AcquirerId = "ACQ-1",
                    AcquirerName = "Updated Name"
                }
            });

        var second = await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var loaded = await terminalStore.GetByLocalDeviceIdAsync(device.Id, CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Single(loaded);
        Assert.Equal("Updated Name", loaded.Single().AcquirerName);
    }

    [Fact]
    public async Task MissingTerminal_InactiveOlur()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-300",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    },
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-2",
                        MerchantId = "MER-2",
                        AcquirerId = "ACQ-2",
                        AcquirerName = "Bank 2"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        client.GetDeviceInfoResponse = BuildDeviceInfoResponse(
            serialNumber: "SN-300",
            deviceName: "PAVO Model X",
            new[]
            {
                new PavoDeviceTerminalInfo
                {
                    TerminalId = "TERM-1",
                    MerchantId = "MER-1",
                    AcquirerId = "ACQ-1",
                    AcquirerName = "Bank 1"
                }
            });

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var loaded = await terminalStore.GetByLocalDeviceIdAsync(device.Id, CancellationToken.None);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, x => x.TerminalId == "TERM-2" && !x.Active);
    }

    [Fact]
    public async Task TerminalStore_SecretIcermez()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-400",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var rawJson = await File.ReadAllTextAsync(CreatePathResolver().LocalDeviceTerminalsStorePath);

        Assert.DoesNotContain("Fingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jwt", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProvisioningCandidate_FingerprintVeAgentBilgisi_Icermez()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-500",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var candidate = await service.BuildProvisioningCandidateAsync(device.Id, 12, new AgentSelfDto
        {
            AgentId = 999,
            KurumId = 77,
            Tesisler =
            [
                new AgentSelfTesisDto { Id = 12, Ad = "TRT / Merkez" }
            ]
        }, CancellationToken.None);

        var json = JsonSerializer.Serialize(candidate, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(12, candidate.TesisId);
        Assert.Single(candidate.Terminals);
        Assert.DoesNotContain("AgentId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("KurumId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EnrollmentCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TransactionSequence", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidTesisSecimi_LocalValidation_Reddedilir()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-600",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildProvisioningCandidateAsync(device.Id, 999, new AgentSelfDto
        {
            AgentId = 1,
            KurumId = 10,
            Tesisler =
            [
                new AgentSelfTesisDto { Id = 12, Ad = "TRT / Merkez" }
            ]
        }, CancellationToken.None));

        Assert.Contains("agent kapsamı", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverySequence_IncrementEder()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-700",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore, transactionSequence: 7);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var state = await pairingStore.GetAsync(device.Id, CancellationToken.None);

        Assert.Equal(8, client.LastGetDeviceInfoRequest?.TransactionHandle.TransactionSequence);
        Assert.Equal(8, state!.TransactionSequence);
    }

    [Fact]
    public async Task RestartSonrasi_TerminalMetadata_Korunur()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-800",
                deviceName: "PAVO Model X",
                new[]
                {
                    new PavoDeviceTerminalInfo
                    {
                        TerminalId = "TERM-1",
                        MerchantId = "MER-1",
                        AcquirerId = "ACQ-1",
                        AcquirerName = "Bank 1"
                    }
                })
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service1 = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service1, store, pairingStore);

        await service1.DiscoverTerminalsAsync(device.Id, CancellationToken.None);

        var service2 = CreateService(client, store, terminalStore, pairingStore);
        var loaded = await service2.GetTerminalsAsync(device.Id, CancellationToken.None);

        Assert.Single(loaded);
        Assert.Equal("TERM-1", loaded.Single().TerminalId);
        Assert.Equal("Bank 1", loaded.Single().AcquirerName);
    }

    private LocalDeviceManagementService CreateService(
        FakePavoRestClient client,
        FileLocalDeviceStore? store = null,
        FileLocalDeviceTerminalStore? terminalStore = null,
        FilePavoLocalPairingStore? pairingStore = null)
    {
        return new LocalDeviceManagementService(
            store ?? CreateStore(),
            terminalStore ?? CreateTerminalStore(),
            new LocalDeviceConnectionTesterRegistry([new FixedTester(LocalDeviceProvider.Pavo)]),
            pairingStore ?? CreatePairingStore(),
            client);
    }

    private FileLocalDeviceStore CreateStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceStore>.Instance);

    private FileLocalDeviceTerminalStore CreateTerminalStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceTerminalStore>.Instance);

    private FilePavoLocalPairingStore CreatePairingStore() =>
        new(CreatePathResolver(), NullLogger<FilePavoLocalPairingStore>.Instance);

    private TempAgentPathResolver CreatePathResolver() => new(_tempDir);

    private async Task<LocalDevice> CreateSavedDeviceAsync(LocalDeviceManagementService service)
    {
        return await service.SaveAsync(new LocalDeviceUpsertRequest
        {
            DisplayName = "PAVO POS",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "192.168.1.50",
            Protocol = LocalDeviceProtocol.Https,
            HttpsPort = 4568,
            HttpPort = 4567,
            SerialNumber = "SN-LOCAL"
        }, CancellationToken.None);
    }

    private async Task<LocalDevice> CreatePairedDeviceAsync(
        LocalDeviceManagementService service,
        FileLocalDeviceStore store,
        FilePavoLocalPairingStore pairingStore,
        long transactionSequence = 0)
    {
        var device = await CreateSavedDeviceAsync(service);
        device.PairingStatus = LocalDevicePairingStatus.Paired;
        device.LastPairingAt = DateTimeOffset.UtcNow;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        await pairingStore.UpsertAsync(new PavoLocalPairingState
        {
            DeviceId = device.Id,
            Fingerprint = "FP-SEED",
            TargetFingerprint = "TFP-SEED",
            TransactionSequence = transactionSequence,
            PairingStatus = LocalDevicePairingStatus.Paired,
            PairingAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            LastPairingAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            UpdatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None);

        return device;
    }

    private static PavoGetDeviceInfoResponse BuildDeviceInfoResponse(string serialNumber, string deviceName, IEnumerable<PavoDeviceTerminalInfo> terminals) =>
        new()
        {
            SerialNumber = serialNumber,
            DeviceName = deviceName,
            Terminals = terminals.ToList(),
            TransactionHandle = new PavoTransactionHandle
            {
                TransactionSequence = 0
            }
        };

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private sealed class FixedTester : ILocalDeviceConnectionTester
    {
        public FixedTester(LocalDeviceProvider provider)
        {
            Provider = provider;
        }

        public LocalDeviceProvider Provider { get; }

        public Task<LocalDeviceConnectionTestResult> TestAsync(LocalDevice device, CancellationToken cancellationToken)
        {
            return Task.FromResult(new LocalDeviceConnectionTestResult
            {
                DeviceId = device.Id,
                Status = LocalDeviceConnectionStatus.Connected,
                Success = true,
                Message = "Bağlantı başarılı.",
                TestedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private sealed class FakePavoRestClient : IPavoRestClient
    {
        public PavoGetDeviceInfoResponse? GetDeviceInfoResponse { get; set; }
        public int GetDeviceInfoCallCount { get; private set; }
        public PavoGetDeviceInfoRequest? LastGetDeviceInfoRequest { get; private set; }

        public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken)
        {
            GetDeviceInfoCallCount++;
            LastGetDeviceInfoRequest = request;
            return Task.FromResult(GetDeviceInfoResponse ?? new PavoGetDeviceInfoResponse());
        }

        public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
