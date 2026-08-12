using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Contracts.Dtos;
using STYS.Agent.LocalDevices;
using STYS.Agent.Modules.Pavo;
using STYS.Agent.Workers;

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
    public async Task TerminalTopolojisi_DegisirIse_ReProvisionRequiredOlur()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-310",
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
        device.CentralPosCihaziId = 6001;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        client.GetDeviceInfoResponse = BuildDeviceInfoResponse(
            serialNumber: "SN-310",
            deviceName: "PAVO Model X",
            new[]
            {
                new PavoDeviceTerminalInfo
                {
                    TerminalId = "TERM-2",
                    MerchantId = "MER-2",
                    AcquirerId = "ACQ-1",
                    AcquirerName = "Bank 1"
                }
            });

        await service.DiscoverTerminalsAsync(device.Id, CancellationToken.None);
        var reloaded = await store.GetByIdAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDeviceProvisioningStatus.ReProvisionRequired, reloaded!.ProvisioningStatus);
    }

    [Fact]
    public async Task StatusSnapshot_FingerprintVeTargetFingerprint_Icermez()
    {
        var snapshot = new AgentPavoDeviceStatusSnapshotDto
        {
            CentralPosCihaziId = 1,
            AgentId = 2,
            KurumId = 3,
            TesisId = 4,
            AgentLocalDeviceId = "local-1",
            Provider = "PAVO",
            SerialNumber = "SN-1",
            Active = true,
            DisplayName = "PAVO POS",
            Host = "192.168.1.50",
            HttpPort = 4567,
            HttpsPort = 4568
        };

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("Fingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckStysStatus_LocalMetadata_OverwriteEdilmez()
    {
        var snapshotClient = new FakeAgentApiClient
        {
            SnapshotResponse = new AgentPavoDeviceStatusSnapshotDto
            {
                CentralPosCihaziId = 9100,
                AgentId = 101,
                KurumId = 202,
                TesisId = 303,
                AgentLocalDeviceId = "local-device-1",
                Provider = "PAVO",
                SerialNumber = "CENTRAL-456",
                Active = true,
                DisplayName = "Central POS",
                Host = "10.10.10.10",
                HttpPort = 9999,
                HttpsPort = 9998
            }
        };
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "LOCAL-123",
                deviceName: "Local Device",
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
        var service = CreateService(client, store, terminalStore, pairingStore, snapshotClient);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);
        device.SerialNumber = "LOCAL-123";
        device.DeviceName = "Local Device";
        device.Host = "192.168.1.50";
        device.CentralPosCihaziId = 9100;
        device.CentralAgentId = 101;
        device.CentralTesisId = 303;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);
        snapshotClient.SnapshotResponse!.AgentLocalDeviceId = device.Id;

        var result = await service.CheckStysStatusAsync(device.Id, CancellationToken.None);
        var reloaded = await store.GetByIdAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDeviceStysReconciliationStatus.ReProvisionRequired, result.Status);
        Assert.Equal("LOCAL-123", reloaded!.SerialNumber);
        Assert.Equal("Local Device", reloaded.DeviceName);
        Assert.Equal("192.168.1.50", reloaded.Host);
        Assert.Equal(4567, reloaded.HttpPort);
        Assert.Equal(4568, reloaded.HttpsPort);
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
    public async Task StaleUpsert_TransactionSequenceIgeriyeCekemez()
    {
        var pairingStore = CreatePairingStore();
        var firstTimestamp = DateTimeOffset.UtcNow.AddMinutes(-2);

        await pairingStore.UpsertAsync(new PavoLocalPairingState
        {
            DeviceId = "device-1",
            Fingerprint = "FP-1",
            TargetFingerprint = "TFP-1",
            TransactionSequence = 12,
            PairingStatus = LocalDevicePairingStatus.Paired,
            PairingAt = firstTimestamp,
            LastPairingAttemptAt = firstTimestamp,
            UpdatedAt = firstTimestamp
        }, CancellationToken.None);

        await pairingStore.UpsertAsync(new PavoLocalPairingState
        {
            DeviceId = "device-1",
            Fingerprint = "FP-STALE",
            TargetFingerprint = "TFP-STALE",
            TransactionSequence = 11,
            PairingStatus = LocalDevicePairingStatus.Failed,
            PairingAt = firstTimestamp.AddMinutes(-1),
            LastPairingAttemptAt = firstTimestamp.AddMinutes(-1),
            LastPairingError = "stale",
            UpdatedAt = firstTimestamp.AddMinutes(-1)
        }, CancellationToken.None);

        var state = await pairingStore.GetAsync("device-1", CancellationToken.None);

        Assert.NotNull(state);
        Assert.Equal(12, state!.TransactionSequence);
        Assert.Equal("FP-1", state.Fingerprint);
        Assert.Equal("TFP-1", state.TargetFingerprint);
        Assert.Equal(LocalDevicePairingStatus.Paired, state.PairingStatus);
    }

    [Fact]
    public async Task LocalVeCentralSequence_AyniStoreUzerindeCakismadanRezervEdilir()
    {
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-900",
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
        var device = await CreatePairedDeviceAsync(service, store, pairingStore, transactionSequence: 0);
        device.SerialNumber = "SN-900";
        device.DeviceName = "PAVO Model X";

        device.CentralPosCihaziId = 7001;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.LastProvisionedAt = DateTimeOffset.UtcNow;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var reservationService = new PavoCommandSequenceReservationService(store, pairingStore);

        var localTask = service.GetDeviceInfoAsync(device.Id, CancellationToken.None);
        var centralTask = reservationService.ReserveAsync(7001, null, CancellationToken.None);
        await Task.WhenAll(localTask, centralTask);

        var localSequence = client.LastGetDeviceInfoRequest?.TransactionHandle.TransactionSequence ?? 0;
        var centralSequence = (await centralTask).TransactionSequence;

        Assert.NotEqual(localSequence, centralSequence);
        Assert.Contains(localSequence, new[] { 1L, 2L });
        Assert.Contains(centralSequence, new[] { 1L, 2L });

        var state = await pairingStore.GetAsync(device.Id, CancellationToken.None);
        Assert.Equal(2, state!.TransactionSequence);

        var restartReservationService = new PavoCommandSequenceReservationService(store, pairingStore);
        var restartHandle = await restartReservationService.ReserveAsync(7001, null, CancellationToken.None);
        Assert.Equal(3, restartHandle.TransactionSequence);
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

    [Fact]
    public async Task RePair_Sonrasi_ProvisionedDevice_ReProvisionRequiredOlur()
    {
        var client = new FakePavoRestClient
        {
            PairingResponse = new PavoPairingResponse
            {
                OnayliMi = true,
                Fingerprint = "FP-NEW",
                TargetFingerprint = "TFP-NEW",
                TransactionHandle = new PavoTransactionHandle
                {
                    TransactionSequence = 4
                }
            }
        };
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(client, store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);
        device.CentralPosCihaziId = 7001;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var updated = await service.PairAsync(device.Id, forceRePair: true, CancellationToken.None);

        Assert.Equal(LocalDevicePairingStatus.Paired, updated.PairingStatus);
        Assert.Equal(LocalDeviceProvisioningStatus.ReProvisionRequired, updated.ProvisioningStatus);
        Assert.Equal(1, client.PairingCallCount);
    }

    [Fact]
    public async Task ReProvisionRequired_CentralCommand_Reddedilir()
    {
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(new FakePavoRestClient(), store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore, transactionSequence: 8);
        device.CentralPosCihaziId = 8001;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.ReProvisionRequired;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var reservationService = new PavoCommandSequenceReservationService(store, pairingStore);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => reservationService.ReserveAsync(8001, null, CancellationToken.None));

        Assert.Contains("uygun değil", ex.Message, StringComparison.OrdinalIgnoreCase);
        var state = await pairingStore.GetAsync(device.Id, CancellationToken.None);
        Assert.Equal(8, state!.TransactionSequence);
    }

    [Fact]
    public async Task Disabled_Cihaz_StysDurumu_DisabledOlur()
    {
        var snapshotClient = new FakeAgentApiClient
        {
            SnapshotResponse = new AgentPavoDeviceStatusSnapshotDto
            {
                CentralPosCihaziId = 9001,
                AgentId = 101,
                KurumId = 202,
                TesisId = 303,
                AgentLocalDeviceId = "local-device-1",
                Provider = "PAVO",
                SerialNumber = "SN-DEVICE",
                Active = false,
                DisplayName = "PAVO POS",
                Host = "192.168.1.50",
                HttpPort = 4567,
                HttpsPort = 4568
            }
        };
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-DEVICE",
                deviceName: "PAVO POS",
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
        var service = CreateService(client, store, terminalStore, pairingStore, snapshotClient);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);
        device.CentralPosCihaziId = 9001;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var result = await service.CheckStysStatusAsync(device.Id, CancellationToken.None);
        var reloaded = await store.GetByIdAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDeviceStysReconciliationStatus.Disabled, result.Status);
        Assert.Equal(LocalDeviceProvisioningStatus.Disabled, reloaded!.ProvisioningStatus);
        Assert.Equal(LocalDeviceStysReconciliationStatus.Disabled, reloaded.StysReconciliationStatus);
    }

    [Fact]
    public async Task AgentLocalDeviceIdMismatch_OwnershipConflictOlur()
    {
        var snapshotClient = new FakeAgentApiClient
        {
            SnapshotResponse = new AgentPavoDeviceStatusSnapshotDto
            {
                CentralPosCihaziId = 9002,
                AgentId = 101,
                KurumId = 202,
                TesisId = 303,
                AgentLocalDeviceId = "different-local-device",
                Provider = "PAVO",
                SerialNumber = "SN-DEVICE-2",
                Active = true,
                DisplayName = "PAVO POS",
                Host = "192.168.1.50",
                HttpPort = 4567,
                HttpsPort = 4568
            }
        };
        var client = new FakePavoRestClient
        {
            GetDeviceInfoResponse = BuildDeviceInfoResponse(
                serialNumber: "SN-DEVICE-2",
                deviceName: "PAVO POS",
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
        var service = CreateService(client, store, terminalStore, pairingStore, snapshotClient);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore);
        device.CentralPosCihaziId = 9002;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Provisioned;
        device.SerialNumber = "SN-DEVICE-2";
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var result = await service.CheckStysStatusAsync(device.Id, CancellationToken.None);

        Assert.Equal(LocalDeviceStysReconciliationStatus.OwnershipConflict, result.Status);
        Assert.Contains("başka Agent'a", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledCihaz_CommandSequenceRezervEtmez()
    {
        var store = CreateStore();
        var terminalStore = CreateTerminalStore();
        var pairingStore = CreatePairingStore();
        var service = CreateService(new FakePavoRestClient(), store, terminalStore, pairingStore);
        var device = await CreatePairedDeviceAsync(service, store, pairingStore, transactionSequence: 11);
        device.CentralPosCihaziId = 9010;
        device.ProvisioningStatus = LocalDeviceProvisioningStatus.Disabled;
        device.UpdatedAt = DateTimeOffset.UtcNow;
        await store.UpdateAsync(device, CancellationToken.None);

        var reservationService = new PavoCommandSequenceReservationService(store, pairingStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reservationService.ReserveAsync(9010, null, CancellationToken.None));
        var state = await pairingStore.GetAsync(device.Id, CancellationToken.None);
        Assert.Equal(11, state!.TransactionSequence);
    }

    private LocalDeviceManagementService CreateService(
        FakePavoRestClient client,
        FileLocalDeviceStore? store = null,
        FileLocalDeviceTerminalStore? terminalStore = null,
        FilePavoLocalPairingStore? pairingStore = null,
        IStysAgentApiClient? agentApiClient = null)
    {
        return new LocalDeviceManagementService(
            store ?? CreateStore(),
            terminalStore ?? CreateTerminalStore(),
            new LocalDeviceConnectionTesterRegistry([new FixedTester(LocalDeviceProvider.Pavo)]),
            pairingStore ?? CreatePairingStore(),
            client,
            agentApiClient);
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
        public int PairingCallCount { get; private set; }
        public PavoPairingResponse? PairingResponse { get; set; }
        public PavoPairingRequest? LastPairingRequest { get; private set; }

        public Task<PavoPairingResponse> PairingAsync(PavoPairingRequest request, CancellationToken cancellationToken)
        {
            PairingCallCount++;
            LastPairingRequest = request;
            return Task.FromResult(PairingResponse ?? new PavoPairingResponse
            {
                OnayliMi = true,
                Fingerprint = request.TransactionHandle.Fingerprint,
                TargetFingerprint = request.CurrentFingerprint,
                TransactionHandle = new PavoTransactionHandle
                {
                    TransactionSequence = request.TransactionHandle.TransactionSequence
                }
            });
        }

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

    private sealed class FakeAgentApiClient : IStysAgentApiClient
    {
        public AgentPavoDeviceStatusSnapshotDto? SnapshotResponse { get; set; }

        public Task<AgentEnrollmentResponse> EnrollAsync(AgentEnrollmentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentTokenResponse> GetTokenAsync(AgentTokenRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SendHeartbeatAsync(AgentHeartbeatRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentConfigDto?> GetConfigurationAsync(long currentVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentSelfDto> GetMeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AgentSelfDto
            {
                AgentId = 101,
                KurumId = 202,
                Tesisler = [new AgentSelfTesisDto { Id = 303, Ad = "Tesis" }]
            });

        public Task<AgentPavoDeviceRegistrationResult> RegisterPavoDeviceAsync(AgentPavoDeviceRegisterRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentPavoDeviceStatusSnapshotDto?> GetPavoDeviceStatusSnapshotAsync(AgentPavoDeviceStatusSnapshotRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(SnapshotResponse);

        public Task<IReadOnlyCollection<AgentCommandDto>> GetPendingCommandsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AcceptCommandAsync(Guid commandId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetRunningCommandAsync(Guid commandId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CompleteCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task FailCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RejectCommandAsync(Guid commandId, AgentCommandCompleteRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
