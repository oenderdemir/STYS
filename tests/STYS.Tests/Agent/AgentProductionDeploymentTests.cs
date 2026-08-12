using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Services;

namespace STYS.Tests.Agent;

public sealed class AgentProductionDeploymentTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "stys-agent-production-tests", Guid.NewGuid().ToString("N"));

    public AgentProductionDeploymentTests()
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
    public void ProductionDi_FileExecutionStoreKullanir()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAgentPathResolver>(new TempAgentPathResolver(_tempDir));
        services.AddAgentProductionInfrastructure();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FileAgentCommandExecutionStore>(provider.GetRequiredService<IAgentCommandExecutionStore>());
    }

    [Fact]
    public async Task UnwritableCriticalStore_StartuplaNotReadyOlur()
    {
        var blockerPath = Path.Combine(_tempDir, "blocked.dat");
        await File.WriteAllTextAsync(blockerPath, "blocked");

        var runtime = new AgentRuntimeStatus();
        var service = new AgentStartupValidationService(
            new FileBackedPathResolver(blockerPath),
            runtime,
            NullLogger<AgentStartupValidationService>.Instance);

        var result = await service.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.False(runtime.StartupHealthy);
        Assert.NotNull(runtime.StartupHealthError);
        Assert.NotNull(runtime.LastStartupValidationAt);
    }

    [Fact]
    public void UninstallScript_DefaultDataKorumaDavranisiIcerir()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "uninstall-agent.ps1");
        Assert.True(File.Exists(scriptPath));

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("[switch]$Purge", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("if ($Purge)", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data", script, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempAgentPathResolver : IAgentPathResolver
    {
        public TempAgentPathResolver(string root) => DataDirectory = root;
        public string DataDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private sealed class FileBackedPathResolver : IAgentPathResolver
    {
        public FileBackedPathResolver(string rootFile) => DataDirectory = rootFile;
        public string DataDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }
}
