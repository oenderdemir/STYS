using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;

namespace STYS.Tests.Agent;

public sealed class AgentCommandExecutionStoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-execution-store-tests", Guid.NewGuid().ToString("N"));

    public AgentCommandExecutionStoreTests()
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
    public void RestartSimulation_MarkerBlocksSecondPhysicalExecution()
    {
        var store1 = CreateStore();
        var store2 = CreateStore();
        var idempotencyKey = "PavoStartPayment:restart-safe";
        var physicalCalls = 0;

        RunPhysicalStartPayment(store1, idempotencyKey, () => physicalCalls++);
        RunPhysicalStartPayment(store2, idempotencyKey, () => physicalCalls++);

        Assert.Equal(1, physicalCalls);
        Assert.True(store2.HasExecuted(idempotencyKey));
        Assert.Null(store2.GetResult(idempotencyKey));
    }

    [Fact]
    public void RestartSimulation_ResultPersistsAcrossInstances()
    {
        var store1 = CreateStore();
        var key = "PavoGetPaymentResult:result";

        store1.MarkExecuted(key);
        store1.StoreResult(key, AgentCommandResult.Ok("{\"status\":\"ok\"}"));

        var store2 = CreateStore();
        Assert.True(store2.HasExecuted(key));
        Assert.Equal("{\"status\":\"ok\"}", store2.GetResult(key)?.ResultPayload);
    }

    [Fact]
    public void CorruptedPersistentFile_FailClosed_AndPhysicalExecutionDoesNotRun()
    {
        File.WriteAllText(StorePath, "{ this is not valid json");
        var store = CreateStore();
        var idempotencyKey = "PavoStartPayment:corrupted";
        var physicalCalls = 0;

        var exception = Record.Exception(() => RunPhysicalStartPayment(store, idempotencyKey, () => physicalCalls++));

        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(0, physicalCalls);
    }

    [Fact]
    public void PairingResult_StaysInMemory_AndSecretsDoNotHitDisk()
    {
        var store = CreateStore();
        var key = "PavoPairing:secret-bearing";

        var payload = JsonSerializer.Serialize(new
        {
            Fingerprint = "fingerprint-secret",
            TargetFingerprint = "target-secret",
            PairingCode = "pairing-secret",
            ClientSecret = "client-secret"
        });

        store.MarkExecuted(key);
        store.StoreResult(key, AgentCommandResult.Ok(payload));

        Assert.True(store.HasExecuted(key));
        Assert.Equal(payload, store.GetResult(key)?.ResultPayload);
        Assert.False(File.Exists(StorePath), "Non-payment commands should not create a persistent execution file.");
    }

    [Fact]
    public async Task ParallelPersistence_DoesNotCorrupt()
    {
        var store1 = CreateStore();
        var store2 = CreateStore();
        var keys = Enumerable.Range(1, 32).Select(i => $"PavoStartPayment:key-{i}").ToArray();

        await Task.WhenAll(keys.Select((key, index) => Task.Run(() =>
        {
            var store = index % 2 == 0 ? store1 : store2;
            store.MarkExecuted(key);
            store.StoreResult(key, AgentCommandResult.Ok($"payload-{index}"));
        })));

        var reloaded = CreateStore();
        foreach (var (key, index) in keys.Select((key, index) => (key, index)))
        {
            Assert.True(reloaded.HasExecuted(key));
            Assert.Equal($"payload-{index}", reloaded.GetResult(key)?.ResultPayload);
        }

        var fileContent = await File.ReadAllTextAsync(StorePath);
        Assert.DoesNotContain("Fingerprint", fileContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TargetFingerprint", fileContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ClientSecret", fileContent, StringComparison.OrdinalIgnoreCase);
    }

    private FileAgentCommandExecutionStore CreateStore() =>
        new(new TempAgentPathResolver(_tempDir), NullLogger<FileAgentCommandExecutionStore>.Instance);

    private string StorePath => Path.Combine(_tempDir, "agent-command-executions.json");

    private static void RunPhysicalStartPayment(IAgentCommandExecutionStore store, string idempotencyKey, Action physicalStart)
    {
        if (store.HasExecuted(idempotencyKey))
        {
            return;
        }

        store.MarkExecuted(idempotencyKey);
        physicalStart();
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
}
