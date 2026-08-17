using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Client.Upgrade;
using STYS.Agent.Contracts.Enums;
using STYS.Agent.Options;
using STYS.Agent.Upgrade;
using Xunit;

namespace STYS.Tests.Agent;

/// <summary>
/// Apply only records a request and defers; the updater service performs the work. These pin that
/// the agent refuses — with a specific error code, not a generic handler exception — whenever that
/// service cannot be confirmed, and that nothing is written in those cases.
/// </summary>
public sealed class AgentApplyUpgradeUpdaterGuardTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-apply-guard", Guid.NewGuid().ToString("N"));
    private const int ReleaseId = 55;
    private const string Version = "1.0.1";
    private const string Rid = "win-x64";
    private const string Sha = "ABCDEF";
    private const string Signature = "c2lnbmF0dXJl";

    public AgentApplyUpgradeUpdaterGuardTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Temp cleanup must not fail a test run.
        }
    }

    private sealed class StubProbe(UpdaterPresence presence) : IUpdaterServicePresenceProbe
    {
        public UpdaterPresence Check(string serviceName) => presence;
    }

    private sealed class RecordingRequestStore : IAgentUpgradeRequestStore
    {
        public AgentApplyUpgradeRequest? Written { get; private set; }
        public Task<AgentApplyUpgradeRequest?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<AgentApplyUpgradeRequest?>(null);
        public Task WriteAsync(AgentApplyUpgradeRequest request, CancellationToken cancellationToken)
        {
            Written = request;
            return Task.CompletedTask;
        }
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StagedStore : IAgentReleaseStagingStore
    {
        public Task<AgentReleaseStagingState?> GetAsync(int releaseId, CancellationToken cancellationToken) =>
            Task.FromResult<AgentReleaseStagingState?>(new AgentReleaseStagingState
            {
                ReleaseId = releaseId,
                Version = Version,
                RuntimeIdentifier = Rid,
                Sha256 = Sha,
                Signature = Signature,
                StageStatus = AgentReleaseStageStatus.Staged
            });

        public Task UpsertAsync(AgentReleaseStagingState state, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedPathResolver(string root) : IAgentPathResolver
    {
        public string DataDirectory { get; } = root;
        public string LogDirectory => Path.Combine(DataDirectory, "logs");
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private (AgentApplyUpgradeCommandHandler Handler, RecordingRequestStore Store) NewHandler(UpdaterPresence presence)
    {
        var store = new RecordingRequestStore();
        IAgentPathResolver paths = new FixedPathResolver(_tempDir);

        // The handler requires the staged package to exist before it consults the updater.
        var packagePath = paths.GetReleaseStagingPackagePath(ReleaseId.ToString(), Rid);
        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        File.WriteAllBytes(packagePath, [1, 2, 3]);

        var handler = new AgentApplyUpgradeCommandHandler(
            store,
            new StagedStore(),
            paths,
            Options.Create(new AgentUpgradeOptions { UpdaterServiceName = "STYS Agent Updater" }),
            new StubProbe(presence),
            NullLogger<AgentApplyUpgradeCommandHandler>.Instance);

        return (handler, store);
    }

    private static AgentApplyUpgradeCommand NewCommand() => new()
    {
        CommandId = Guid.NewGuid(),
        ReleaseId = ReleaseId,
        Version = Version,
        RuntimeIdentifier = Rid,
        Sha256 = Sha,
        Signature = Signature
    };

    // ---------------------------------------------------------------- D. updater missing

    [Fact]
    public async Task UpdaterKurulDegilse_AgentUpdaterNotAvailableDoner()
    {
        var (handler, store) = NewHandler(UpdaterPresence.Missing);

        var result = await handler.HandleAsync(NewCommand(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AgentApplyUpgradeCommandHandler.UpdaterNotAvailableCode, result.ErrorCode);
        Assert.False(result.DeferCompletion);
        Assert.Null(store.Written);
    }

    // ---------------------------------------------------------------- E. status unknown

    [Fact]
    public async Task UpdaterDurumuBilinmiyorsa_FailClosedVeIstekYazilmaz()
    {
        var (handler, store) = NewHandler(UpdaterPresence.Unknown);

        var result = await handler.HandleAsync(NewCommand(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(AgentApplyUpgradeCommandHandler.UpdaterStatusUnknownCode, result.ErrorCode);
        Assert.False(result.DeferCompletion);

        // Nothing may be left behind: a written request with no updater to read it is the silent
        // hang this guard exists to prevent.
        Assert.Null(store.Written);
    }

    // ---------------------------------------------------------------- F. happy path preserved

    [Fact]
    public async Task UpdaterMevcutsa_IstekYazilirVeTamamlanmaErtelenir()
    {
        var (handler, store) = NewHandler(UpdaterPresence.Present);
        var command = NewCommand();

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.DeferCompletion);
        Assert.NotNull(store.Written);
        Assert.Equal(command.CommandId, store.Written!.CommandId);
        Assert.Equal(ReleaseId, store.Written.ReleaseId);
    }

    [Fact]
    public async Task HataKodlari_GenerikOlmayanSabitDegerler()
    {
        // CommandPollingWorker reports uncaught exceptions as HANDLER_EXCEPTION, which would hide
        // the real cause from command history; these must arrive as explicit codes instead.
        Assert.Equal("AGENT_UPDATER_NOT_AVAILABLE", AgentApplyUpgradeCommandHandler.UpdaterNotAvailableCode);
        Assert.Equal("AGENT_UPDATER_STATUS_UNKNOWN", AgentApplyUpgradeCommandHandler.UpdaterStatusUnknownCode);

        var (handler, _) = NewHandler(UpdaterPresence.Missing);
        var result = await handler.HandleAsync(NewCommand(), CancellationToken.None);
        Assert.NotEqual("HANDLER_EXCEPTION", result.ErrorCode);
    }
}
