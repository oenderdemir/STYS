using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
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
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);
        var logDir = Path.Combine(_tempDir, "blocked-log.dat");
        await File.WriteAllTextAsync(logDir, "blocked");

        var runtime = new AgentRuntimeStatus();
        var service = new AgentStartupValidationService(
            new OverridePathResolver(dataDir, logDir),
            runtime,
            NullLogger<AgentStartupValidationService>.Instance);

        var result = await service.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.False(runtime.StartupHealthy);
        Assert.NotNull(runtime.StartupHealthError);
        Assert.NotNull(runtime.LastStartupValidationAt);
        Assert.Contains(logDir, result.CheckedPaths);
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

    [Fact]
    public void WindowsInstallScript_DirectExeBinPath_And_NoWrapper()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "install-agent.ps1");
        Assert.True(File.Exists(scriptPath));

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("STYS.Agent.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("binPath=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Grant-DirectoryAccess -Path $InstallDir -Identity $ServiceAccount -Rights 'RX'", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_DATA_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_LOG_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_LOCAL_UI_PORT", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-STYS-Agent.ps1", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell.exe", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxInstallScript_InstallDirRootOwned_And_DataLogOverridesSet()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "install-agent.sh");
        Assert.True(File.Exists(scriptPath));

        var script = File.ReadAllText(scriptPath);
        Assert.DoesNotContain("$LocalUiPort", script, StringComparison.Ordinal);
        Assert.Contains("LOCAL_UI_PORT=\"${5:-5180}\"", script, StringComparison.Ordinal);
        Assert.Contains("chown -R root:root \"$INSTALL_DIR\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod -R u=rwX,go=rX \"$INSTALL_DIR\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_DATA_DIR=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_LOG_DIR=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_LOCAL_UI_PORT=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=ASPNETCORE_URLS=http://127.0.0.1:$LOCAL_UI_PORT", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chown -R \"$SERVICE_USER:$SERVICE_USER\" \"$INSTALL_DIR\"", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxInstallScript_BashSyntaxIsValid()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "install-agent.sh");
        var bashScriptPath = ToBashPath(scriptPath);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-n \"{bashScriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(process);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            var stdout = process.StandardOutput.ReadToEnd();
            Assert.Fail($"bash -n failed with exit code {process.ExitCode}. stderr: {stderr} stdout: {stdout}");
        }
    }

    [Fact]
    public void LinuxInstallScript_CustomPortIsReflectedInUnitTemplate()
    {
        var port = 6123;
        var tempScript = Path.Combine(_tempDir, "render-unit.sh");
        File.WriteAllText(tempScript, $$"""
#!/usr/bin/env bash
set -euo pipefail
LOCAL_UI_PORT={{port}}
printf 'Environment=STYS_AGENT_LOCAL_UI_PORT=%s\nEnvironment=ASPNETCORE_URLS=http://127.0.0.1:%s\n' "$LOCAL_UI_PORT" "$LOCAL_UI_PORT"
""");

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"\"{ToBashPath(tempScript)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.Empty(stderr);
        Assert.Contains($"Environment=STYS_AGENT_LOCAL_UI_PORT={port}", stdout, StringComparison.Ordinal);
        Assert.Contains($"Environment=ASPNETCORE_URLS=http://127.0.0.1:{port}", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void AgentPathResolver_UsesProductionDirectoryOverrides()
    {
        var dataDir = Path.Combine(_tempDir, "custom-data");
        var logDir = Path.Combine(_tempDir, "custom-logs");

        SetEnvironment("STYS_AGENT_DATA_DIR", dataDir);
        SetEnvironment("STYS_AGENT_LOG_DIR", logDir);

        try
        {
            var resolver = new AgentPathResolver();

            Assert.Equal(Path.GetFullPath(dataDir), resolver.DataDirectory);
            Assert.Equal(Path.GetFullPath(logDir), resolver.LogDirectory);
        }
        finally
        {
            SetEnvironment("STYS_AGENT_DATA_DIR", null);
            SetEnvironment("STYS_AGENT_LOG_DIR", null);
        }
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

    private sealed class FileBackedPathResolver : IAgentPathResolver
    {
        public FileBackedPathResolver(string rootFile) => DataDirectory = rootFile;
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

    private sealed class OverridePathResolver : IAgentPathResolver
    {
        public OverridePathResolver(string dataDirectory, string logDirectory)
        {
            DataDirectory = dataDirectory;
            LogDirectory = logDirectory;
        }

        public string DataDirectory { get; }
        public string LogDirectory { get; }
        public string BootstrapConfigurationPath => Path.Combine(DataDirectory, "bootstrap.json");
        public string CredentialStorePath => Path.Combine(DataDirectory, "credential.dat");
        public string LocalDevicesStorePath => Path.Combine(DataDirectory, "local-devices.json");
        public string LocalDeviceTerminalsStorePath => Path.Combine(DataDirectory, "local-device-terminals.json");
        public string PavoPairingStorePath => Path.Combine(DataDirectory, "pavo-pairing.dat");
        public string AgentCommandExecutionStorePath => Path.Combine(DataDirectory, "agent-command-executions.json");
        public string InstanceIdPath => Path.Combine(DataDirectory, "instance.id");
    }

    private static void SetEnvironment(string name, string? value) =>
        Environment.SetEnvironmentVariable(name, value);

    private static string ToBashPath(string path)
    {
        var fullPath = Path.GetFullPath(path).Replace('\\', '/');
        if (fullPath.Length > 1 && fullPath[1] == ':')
        {
            return "/mnt/" + char.ToLowerInvariant(fullPath[0]) + fullPath[2..];
        }

        return fullPath;
    }
}
