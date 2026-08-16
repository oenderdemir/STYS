using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using STYS.Agent.Client.Commands;
using STYS.Agent.Client.Infrastructure;
using STYS.Agent.Services;

namespace STYS.Tests.Agent;

/// <summary>
/// Building a unified installer package needs the win-x64 publish output and a provisioned release
/// trust anchor, neither of which exists on a clean checkout. Skip rather than fail when the local
/// environment has not produced them.
/// </summary>
public sealed class InstallerPackageFactAttribute : FactAttribute
{
    public InstallerPackageFactAttribute()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var deployRoot = Path.Combine(repoRoot, "artifacts", "deploy", "win-x64");

        foreach (var kind in new[] { "agent", "updater" })
        {
            var directory = Path.Combine(deployRoot, kind);
            if (!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any())
            {
                Skip = $"win-x64 {kind} publish ciktisi yok ({directory}) — installer paketi testi atlandi.";
                return;
            }
        }

        var trustAnchorAvailable =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM"))
            || File.Exists(Environment.GetEnvironmentVariable("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH") ?? string.Empty)
            || File.Exists(Path.Combine(deployRoot, "..", "trust", "release-public-key.pem"))
            || File.Exists(Path.Combine(repoRoot, "trust", "release-public-key.pem"));

        if (!trustAnchorAvailable)
        {
            Skip = "release trust anchor provision edilmemis — installer paketi testi atlandi.";
        }
    }
}

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
        Assert.Contains("STYS_AGENT_SHARED_DATA_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_UPDATER_PRIVATE_DATA_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS\\AgentUpdater\\private", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS\\AgentTrust\\release-public-key.pem", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_LOG_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_LOCAL_UI_PORT", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Start-STYS-Agent.ps1", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell.exe", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowsUpdaterInstallScript_DirectExeBinPath_And_ServiceSeparation()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.ps1");
        Assert.True(File.Exists(scriptPath));

        var script = File.ReadAllText(scriptPath);
        Assert.Contains("STYS.Agent.Updater.exe", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS Agent Updater", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LocalSystem", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdaterInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AgentInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_UPDATER_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_SHARED_DATA_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_UPDATER_PRIVATE_DATA_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS\\AgentUpdater\\private", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS\\AgentTrust\\release-public-key.pem", script, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("LOCAL_UI_PORT=\"${6:-5180}\"", script, StringComparison.Ordinal);
        Assert.Contains("chown -R root:root \"$INSTALL_DIR\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod -R u=rwX,go=rX \"$INSTALL_DIR\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_SHARED_DATA_DIR=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_UPDATER_PRIVATE_DATA_DIR=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_RELEASE_PUBLIC_KEY_PEM_PATH=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_LOG_DIR=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_LOCAL_UI_PORT=", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=ASPNETCORE_URLS=http://127.0.0.1:$LOCAL_UI_PORT", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/etc/stys-agent/trust/release-public-key.pem", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0755 \"$TRUST_DIR\"", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0644 \"$RELEASE_PUBLIC_KEY_PATH\"", script, StringComparison.OrdinalIgnoreCase);
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
    public void LinuxUpdaterInstallScript_BashSyntaxIsValid()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var scriptPath = Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.sh");
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
    public void WindowsUpdaterInstallScript_TargetAgentDirDiffersFromUpdaterDir()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.ps1"));

        Assert.Contains("UpdaterInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AgentInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_UPDATER_INSTALL_DIR=$UpdaterInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STYS_AGENT_INSTALL_DIR=$AgentInstallDir", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("STYS_AGENT_INSTALL_DIR=$UpdaterInstallDir", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxUpdaterInstallScript_TargetAgentDirDiffersFromUpdaterDir_AndStaticUnitMatches()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.sh"));
        var unit = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "stys-agent-updater.service"));

        Assert.Contains("UPDATER_INSTALL_DIR=\"${2:-/opt/stys-agent-updater}\"", script, StringComparison.Ordinal);
        Assert.Contains("AGENT_INSTALL_DIR=\"${3:-/opt/stys-agent}\"", script, StringComparison.Ordinal);
        Assert.Contains("Environment=STYS_AGENT_UPDATER_INSTALL_DIR=$UPDATER_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_INSTALL_DIR=$AGENT_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_UPDATER_INSTALL_DIR=/opt/stys-agent-updater", unit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Environment=STYS_AGENT_INSTALL_DIR=/opt/stys-agent", unit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxTrustAnchorCommands_AreRootOwnedAndReadonly()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var agentScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "install-agent.sh"));
        var updaterScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.sh"));

        Assert.Contains("chown -R root:root \"$TRUST_DIR\"", agentScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0755 \"$TRUST_DIR\"", agentScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0644 \"$RELEASE_PUBLIC_KEY_PATH\"", agentScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chown -R root:root \"$TRUST_DIR\"", updaterScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0755 \"$TRUST_DIR\"", updaterScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chmod 0644 \"$RELEASE_PUBLIC_KEY_PATH\"", updaterScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdaterBackupAndReplaceTarget_AgentInstallDirOlarakAyrilir()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "agent", "install-agent-updater.sh"));

        Assert.Contains("Environment=STYS_AGENT_INSTALL_DIR=$AGENT_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Environment=STYS_AGENT_INSTALL_DIR=$UPDATER_INSTALL_DIR", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinuxUpdaterInstallScript_CustomPortIsReflectedInUnitTemplate()
    {
        var port = 6124;
        var tempScript = Path.Combine(_tempDir, "render-updater-unit.sh");
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
            Assert.Contains("AgentUpdater", resolver.UpdaterPrivateDataDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("private", resolver.UpdaterPrivateDataDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.False(Path.GetFullPath(resolver.UpdaterPrivateDataDirectory).StartsWith(Path.GetFullPath(resolver.DataDirectory), StringComparison.OrdinalIgnoreCase));
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
        public string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
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
        public string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
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
        public string ReleaseStagingRootDirectory => Path.Combine(DataDirectory, "updates", "staging");
        public string GetReleaseStagingDirectory(string version, string runtimeIdentifier) => Path.Combine(ReleaseStagingRootDirectory, version, runtimeIdentifier);
        public string GetReleaseStagingStatePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "staging-state.json");
        public string GetReleaseStagingPackagePath(string version, string runtimeIdentifier) => Path.Combine(GetReleaseStagingDirectory(version, runtimeIdentifier), "package.bin");
    }

    // ---------------------------------------------------------------- PowerShell installer syntax

    public static TheoryData<string> WindowsInstallerScripts() =>
    [
        "install-stys-agent.ps1",
        "install-agent.ps1",
        "install-agent-updater.ps1",
        "uninstall-agent.ps1",
        "uninstall-agent-updater.ps1"
    ];

    [Theory]
    [MemberData(nameof(WindowsInstallerScripts))]
    public void WindowsInstallerScript_PowerShellSyntaxIsValid(string scriptName)
    {
        var scriptPath = Path.Combine(RepositoryRoot(), "scripts", "agent", scriptName);
        Assert.True(File.Exists(scriptPath), $"script bulunamadı: {scriptPath}");

        // The interpolation bug this guards ("$Identity:($Rights)") is a parse error, so the script
        // fails before its first statement runs. Both hosts are checked because the operator may
        // launch the installer from Windows PowerShell 5.1 or from pwsh 7.
        var hosts = new[] { "powershell", "pwsh" }.Where(IsHostAvailable).ToArray();
        Assert.NotEmpty(hosts);

        foreach (var shell in hosts)
        {
            var (exitCode, output) = RunPowerShell(shell,
                $"$e=$null;$t=$null;[void][System.Management.Automation.Language.Parser]::ParseFile('{scriptPath.Replace("'", "''")}',[ref]$t,[ref]$e);" +
                "if($e -and $e.Count -gt 0){$e|%{Write-Output (\"line {0}: {1}\" -f $_.Extent.StartLineNumber,$_.Message)};exit 1}else{exit 0}");

            Assert.True(exitCode == 0, $"{shell} parser {scriptName} dosyasında hata buldu:{Environment.NewLine}{output}");
        }
    }

    [Theory]
    [MemberData(nameof(WindowsInstallerScripts))]
    public void WindowsInstallerScript_ColonAfterVariableIsBraceDelimited(string scriptName)
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "scripts", "agent", scriptName));

        // Comment lines are skipped so that documenting the broken form next to its fix does not
        // trip this check.
        var code = string.Join('\n', script
            .Split('\n')
            .Where(line => !line.TrimStart().StartsWith('#')));

        // "$ServiceAccount:R" is NOT a parse error — PowerShell reads it as scope "ServiceAccount",
        // variable "R", and expands it to an empty string, so icacls silently drops the ACE. The
        // parser test above cannot catch that variant, which is why this pattern check exists.
        var offenders = System.Text.RegularExpressions.Regex
            .Matches(code, @"\$(?!env:|script:|global:|local:|using:)[A-Za-z_][A-Za-z0-9_]*:")
            .Select(m => m.Value)
            .Distinct()
            .ToArray();

        Assert.True(offenders.Length == 0,
            $"{scriptName}: değişken adından hemen sonra ':' geliyor, ${{}} ile sınırlandırılmalı -> {string.Join(", ", offenders)}");
    }

    [Theory]
    [MemberData(nameof(WindowsInstallerScripts))]
    public void WindowsInstallerScript_HasUtf8Bom(string scriptName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot(), "scripts", "agent", scriptName));

        // Without a BOM, Windows PowerShell 5.1 decodes the file using the active ANSI codepage and
        // Turkish literals render as mojibake ("başlıyor" -> "baÅŸlÄ±yor").
        Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"{scriptName}: UTF-8 BOM yok.");
    }

    [InstallerPackageFact]
    public void UnifiedPackage_ScriptsAreParserSafeAndBomEncoded()
    {
        var session = new STYS.Agent.Entities.AgentInstallationSession
        {
            Id = 1,
            KurumId = 1,
            TesisId = 1,
            AgentDisplayName = "Paket Doğrulama Agent",
            TargetRid = "win-x64"
        };

        var bytes = AgentInstallerPackageBuilder.Build(session, "https://stys.example");

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        var scriptEntries = archive.Entries
            .Where(e => e.FullName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(scriptEntries);

        foreach (var entry in scriptEntries)
        {
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            var raw = buffer.ToArray();

            // The repository copies carry a BOM, but File.ReadAllText strips it on the way in, so
            // the packaged copy only keeps one if the builder writes it back.
            Assert.True(raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF,
                $"{entry.FullName}: pakete giren kopyada UTF-8 BOM yok.");

            var text = new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
            var code = string.Join('\n', text.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));

            Assert.DoesNotMatch(@"\$(?!env:|script:|global:|local:|using:)[A-Za-z_][A-Za-z0-9_]*:", code);
        }

        // bootstrap.json must NOT gain a BOM: System.Text.Json rejects it as an invalid start value.
        var bootstrapEntry = archive.Entries.Single(e => e.FullName.EndsWith("bootstrap.json", StringComparison.OrdinalIgnoreCase));
        using var bootstrapStream = bootstrapEntry.Open();
        using var bootstrapBuffer = new MemoryStream();
        bootstrapStream.CopyTo(bootstrapBuffer);
        var bootstrapBytes = bootstrapBuffer.ToArray();

        Assert.False(bootstrapBytes.Length >= 3 && bootstrapBytes[0] == 0xEF && bootstrapBytes[1] == 0xBB && bootstrapBytes[2] == 0xBF,
            "bootstrap.json BOM ile yazılmış; JSON okuyucu bunu reddeder.");
        System.Text.Json.JsonDocument.Parse(bootstrapBytes);
    }

    private static string RepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static bool IsHostAvailable(string shell)
    {
        try
        {
            var (exitCode, _) = RunPowerShell(shell, "exit 0");
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunPowerShell(string shell, string command)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = shell,
            ArgumentList = { "-NoProfile", "-NonInteractive", "-Command", command },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"{shell} başlatılamadı.");

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
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
