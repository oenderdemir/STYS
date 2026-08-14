using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;

namespace STYS.Tests.Agent;

public sealed class AgentLocalDevicesPhaseB1Tests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-b1-tests", Guid.NewGuid().ToString("N"));

    public AgentLocalDevicesPhaseB1Tests()
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
    public async Task PrinterPavo_DiscoveryVePairing_Reddedilir()
    {
        var client = new FakePavoRestClient();
        var service = CreateService(client);
        var printer = await CreateSavedDeviceAsync(service, LocalDeviceType.Printer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDeviceInfoAsync(printer.Id, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PairAsync(printer.Id, forceRePair: false, CancellationToken.None));
    }

    [Fact]
    public async Task GetDeviceInfo_Success_PublicMetadataVeSecureState_Gunceller()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = new PavoGetDeviceInfoResponse
            {
                DeviceName = "PAVO X-100",
                SerialNumber = "SN-123",
                Fingerprint = "FP-123",
                TargetFingerprint = "TFP-123",
                TransactionHandle = new PavoTransactionHandle
                {
                    TransactionSequence = 0
                }
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        var updated = await service.GetDeviceInfoAsync(device.Id, CancellationToken.None);
        var pairingState = await CreatePairingStore().GetAsync(device.Id, CancellationToken.None);
        var rawJson = await File.ReadAllTextAsync(CreatePathResolver().LocalDevicesStorePath);

        Assert.Equal("SN-123", updated.SerialNumber);
        Assert.Equal("PAVO X-100", updated.DeviceName);
        Assert.Equal(LocalDevicePairingStatus.NotPaired, updated.PairingStatus);
        Assert.NotNull(updated.LastDeviceInfoAt);
        Assert.NotNull(pairingState);
        // The client fingerprint sent/stored is always our own stable, configured value - never
        // something learned from the device's (legacy/diagnostic-only) response.Fingerprint field.
        Assert.Equal("STYS.Agent", pairingState!.Fingerprint);
        Assert.Equal("TFP-123", pairingState.TargetFingerprint);
        Assert.Equal(1, client.GetDeviceInfoCallCount);
        Assert.Equal(1, client.LastGetDeviceInfoRequest?.TransactionHandle.TransactionSequence);
        Assert.Equal("STYS.Agent", client.LastGetDeviceInfoRequest?.TransactionHandle.Fingerprint);
        Assert.DoesNotContain("FP-123", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TFP-123", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDeviceInfo_Error_PublicStateyi_Bozmaz()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = new PavoGetDeviceInfoResponse
            {
                HasError = true,
                Message = "PAVO cihaz hatası"
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDeviceInfoAsync(device.Id, CancellationToken.None));

        var loaded = await CreateStore().GetByIdAsync(device.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Null(loaded!.DeviceName);
        Assert.Null(loaded.LastDeviceInfoAt);
    }

    [Fact]
    public async Task PairAsync_GetDeviceInfoOlmadanCalisirVeIlkSequenceBirdir()
    {
        // Reference flow: Local PAVO device -> SerialNumber mevcut -> Pairing -> GetDeviceInfo.
        // GetDeviceInfo must NOT be a pairing prerequisite anymore.
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false,
                ErrorCode = null,
                OnayliMi = false, // must be irrelevant to success now
                TransactionHandle = new PavoTransactionHandle { TransactionSequence = 0 }
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        var updated = await service.PairAsync(device.Id, forceRePair: false, CancellationToken.None);
        var pairingState = await CreatePairingStore().GetAsync(device.Id, CancellationToken.None);

        Assert.Equal(0, client.GetDeviceInfoCallCount);
        Assert.Equal(1, client.PairingCallCount);
        Assert.Equal(LocalDevicePairingStatus.Paired, updated.PairingStatus);
        Assert.NotNull(pairingState);
        Assert.Equal(LocalDevicePairingStatus.Paired, pairingState!.PairingStatus);

        // First-ever PAVO interaction for this device must be sequence 1.
        Assert.Equal(1, client.LastPairingRequest?.TransactionHandle.TransactionSequence);
        Assert.Equal(1, pairingState.TransactionSequence);

        // Client fingerprint is our own stable, configured identity (not learned from the device).
        Assert.Equal("STYS.Agent", client.LastPairingRequest?.TransactionHandle.Fingerprint);
        Assert.Equal("STYS.Agent", client.LastPairingRequest?.CurrentFingerprint);
        Assert.Equal("STYS.Agent", pairingState.Fingerprint);
    }

    [Fact]
    public async Task PairAsync_OnayliMiFalseAmaBaseKriterUyuyorsaBasarilidir()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false,
                ErrorCode = 0,
                OnayliMi = false,
                Message = "ok"
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        var updated = await service.PairAsync(device.Id, forceRePair: false, CancellationToken.None);

        Assert.Equal(LocalDevicePairingStatus.Paired, updated.PairingStatus);
    }

    [Fact]
    public async Task PairAsync_ErrorCodeSifirDegilse_Basarisizdir()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false,
                ErrorCode = 7,
                OnayliMi = true,
                Message = "PAVO hata 7"
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PairAsync(device.Id, forceRePair: false, CancellationToken.None));

        Assert.Contains("PAVO hata 7", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PairAsync_SerialNumberYoksa_AcikHataVerir()
    {
        var client = new FakePavoRestClient();
        var service = CreateService(client);
        var device = await service.SaveAsync(new LocalDeviceUpsertRequest
        {
            DisplayName = "PAVO POS Seri Yok",
            DeviceType = LocalDeviceType.Pos,
            Provider = LocalDeviceProvider.Pavo,
            Host = "192.168.1.51",
            Protocol = LocalDeviceProtocol.Https,
            HttpsPort = 4568,
            HttpPort = 4567
        }, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PairAsync(device.Id, forceRePair: false, CancellationToken.None));

        Assert.Contains("seri numarası zorunludur", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.PairingCallCount);
    }

    [Fact]
    public async Task RePair_ForceOlmadan_Reddedilir_veMevcutState_Korunur()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        await service.PairAsync(device.Id, forceRePair: false, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PairAsync(device.Id, forceRePair: false, CancellationToken.None));
        Assert.Equal(1, client.PairingCallCount);

        var loaded = await CreateStore().GetByIdAsync(device.Id, CancellationToken.None);
        var pairingState = await CreatePairingStore().GetAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDevicePairingStatus.Paired, loaded!.PairingStatus);
        Assert.Equal(LocalDevicePairingStatus.Paired, pairingState!.PairingStatus);
    }

    [Fact]
    public async Task FailedRePair_MevcutBasariliStatei_Bozmaz()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        await service.PairAsync(device.Id, forceRePair: false, CancellationToken.None);

        client.PairingResponse = new PavoPairingResponse
        {
            HasError = true,
            Message = "Yeniden eşleştirme başarısız."
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PairAsync(device.Id, forceRePair: true, CancellationToken.None));
        Assert.Contains("başarısız", ex.Message, StringComparison.OrdinalIgnoreCase);

        var loaded = await CreateStore().GetByIdAsync(device.Id, CancellationToken.None);
        var pairingState = await CreatePairingStore().GetAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDevicePairingStatus.Paired, loaded!.PairingStatus);
        Assert.Equal(LocalDevicePairingStatus.Paired, pairingState!.PairingStatus);
        Assert.Equal("STYS.Agent", pairingState.Fingerprint);
        Assert.Equal("Yeniden eşleştirme başarısız.", loaded.LastPairingError);
        Assert.Equal("Yeniden eşleştirme başarısız.", pairingState.LastPairingError);
    }

    [Fact]
    public async Task RestartSonrasi_PairingStateVeSequence_Korunur()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                HasError = false,
                HasAbondon = false
            }
        };
        var service1 = CreateService(client);
        var device = await CreateSavedDeviceAsync(service1);

        await service1.PairAsync(device.Id, forceRePair: false, CancellationToken.None);

        var service2 = CreateService(client);
        var loaded = await service2.GetByIdAsync(device.Id, CancellationToken.None);
        var pairingState = await CreatePairingStore().GetAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDevicePairingStatus.Paired, loaded!.PairingStatus);
        Assert.Equal(LocalDevicePairingStatus.Paired, pairingState!.PairingStatus);
        Assert.Equal("STYS.Agent", pairingState.Fingerprint);
        Assert.Equal(1, pairingState.TransactionSequence);
    }

    [Fact]
    public async Task ParallelSequenceReservation_UniqueCalisir()
    {
        var store = CreatePairingStore();
        var deviceId = Guid.NewGuid().ToString("N");

        var sequences = await Task.WhenAll(Enumerable.Range(0, 16).Select(async _ => (await store.ReserveNextTransactionSequenceAsync(deviceId, CancellationToken.None)).TransactionSequence));

        Assert.Equal(16, sequences.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 16).Select(x => (long)x).ToArray(), sequences.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Fingerprint_ResponseVeLocalJson_IcerisineSizmaz()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = new PavoGetDeviceInfoResponse
            {
                DeviceName = "PAVO X",
                SerialNumber = "SN-SECURE",
                Fingerprint = "FP-SECRET",
                TargetFingerprint = "TFP-SECRET"
            }
        };
        var service = CreateService(client);
        var device = await CreateSavedDeviceAsync(service);

        var updated = await service.GetDeviceInfoAsync(device.Id, CancellationToken.None);
        var serialized = JsonSerializer.Serialize(updated, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var rawJson = await File.ReadAllTextAsync(CreatePathResolver().LocalDevicesStorePath);

        Assert.DoesNotContain("FP-SECRET", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TFP-SECRET", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Fingerprint", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FP-SECRET", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TFP-SECRET", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    private LocalDeviceManagementService CreateService(FakePavoRestClient client, FileLocalDeviceStore? store = null, FilePavoLocalPairingStore? pairingStore = null)
    {
        return new LocalDeviceManagementService(
            store ?? CreateStore(),
            CreateTerminalStore(),
            new LocalDeviceConnectionTesterRegistry([new FixedTester(LocalDeviceProvider.Pavo)]),
            pairingStore ?? CreatePairingStore(),
            client);
    }

    private FileLocalDeviceStore CreateStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceStore>.Instance);

    private FilePavoLocalPairingStore CreatePairingStore() =>
        new(CreatePathResolver(), NullLogger<FilePavoLocalPairingStore>.Instance);

    private FileLocalDeviceTerminalStore CreateTerminalStore() =>
        new(CreatePathResolver(), NullLogger<FileLocalDeviceTerminalStore>.Instance);

    private TempAgentPathResolver CreatePathResolver() => new(_tempDir);

    private async Task<LocalDevice> CreateSavedDeviceAsync(LocalDeviceManagementService service, LocalDeviceType deviceType = LocalDeviceType.Pos)
    {
        return await service.SaveAsync(new LocalDeviceUpsertRequest
        {
            DisplayName = deviceType == LocalDeviceType.Pos ? "PAVO POS" : "PAVO Printer",
            DeviceType = deviceType,
            Provider = LocalDeviceProvider.Pavo,
            Host = "192.168.1.50",
            Protocol = LocalDeviceProtocol.Https,
            HttpsPort = 4568,
            HttpPort = 4567,
            SerialNumber = "SN-LOCAL"
        }, CancellationToken.None);
    }

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
        public PavoPairingResponse? PairingResponse { get; set; }
        public Exception? GetDeviceInfoException { get; set; }
        public Exception? PairingException { get; set; }
        public int GetDeviceInfoCallCount { get; private set; }
        public int PairingCallCount { get; private set; }
        public PavoGetDeviceInfoRequest? LastGetDeviceInfoRequest { get; private set; }
        public PavoPairingRequest? LastPairingRequest { get; private set; }

        public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken)
        {
            PairingCallCount++;
            LastPairingRequest = request;
            if (PairingException is not null)
            {
                throw PairingException;
            }

            return Task.FromResult(PairingResponse ?? new PavoPairingResponse());
        }

        public Task<PavoPingResponse> PingAsync(PavoPingRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetDeviceInfoResponse> GetDeviceInfoAsync(PavoGetDeviceInfoRequest request, CancellationToken cancellationToken)
        {
            GetDeviceInfoCallCount++;
            LastGetDeviceInfoRequest = request;
            if (GetDeviceInfoException is not null)
            {
                throw GetDeviceInfoException;
            }

            return Task.FromResult(GetDeviceInfoResponse ?? new PavoGetDeviceInfoResponse());
        }

        public Task<PavoStartPaymentResponse> StartPaymentAsync(PavoStartPaymentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PavoGetPaymentResultResponse> GetPaymentResultAsync(PavoGetPaymentResultRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
